using System;
using System.Collections;
using Baseline.Defs;
using Baseline.Dialogue;
using Baseline.Presentation;
using Baseline.Quests;
using Hypocycloid.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Baseline.Demo.RelayRun
{
    public sealed class DemoRunnerGame : MonoBehaviour
    {
        [Serializable]
        public sealed class ScrollLayer
        {
            public RectTransform first, second;
            public float width = 1600;
            public float speedFactor = .1f;
            [NonSerialized] public Vector2 origin;
        }

        public DefDatabase database;
        public DemoRunnerDef definition;
        public RectTransform runner;
        public Image runnerImage;
        public Color failureTint;
        public DemoRexAnimation rexAnimation;
        public DemoRexFootFx footFx;
        public Image[] obstacleViews;
        public Sprite[] obstacleSprites;
        public TMP_Text distanceText, bestText, clearedText, resultDistanceText;
        public Image progressFill;
        public GameObject readyPanel, pausePanel, resultPanel, successHeading, failureHeading;
        public Button startButton, jumpButton, pauseButton, resumeButton, retryButton, homeButton, resultHomeButton;
        public Button topHomeButton;
        public ScrollLayer[] scenery;
        public DialogDef briefingDialog, failureDialog, successDialog;
        public SoundCueDef jumpCue, crashCue, successCue;
        public FxEventDef jumpFx, crashFx, successFx;
        public LocalizedString abandonHeading, abandonBody, abandonAccept;
        public LocalizedString lockedHeading, lockedBody;

        DemoRunnerModel model;
        QuestRun quest;
        DialoguePanel dialogue;
        Color runnerTint;
        bool ready, paused, running, awaitingBriefing, awaitingOutcome, confirmingExit;
        int reportedMetres, reportedClears, best;
        public DemoRunnerModel Model => model;
        public QuestRun Quest => quest;
        public bool IsPaused => paused;
        public DialoguePanel Dialogue => dialogue;

        IEnumerator Start()
        {
            if (!ContextManager.HasInstance)
            {
                SceneManager.LoadSceneAsync("Index");
                yield break;
            }
            float waitUntil = Time.realtimeSinceStartup + 30;
            while (!DemoPresentationBootstrap.IsReady && Time.realtimeSinceStartup < waitUntil) yield return null;
            if (!DemoPresentationBootstrap.IsReady)
                throw new InvalidOperationException("Relay Run presentation did not finish loading. Enter through Index.");
            database.Validate();
            if (database.Find<DemoRunnerDef>(definition.DefName) != definition
                || database.Find<QuestDef>(definition.quest.DefName) != definition.quest)
                throw new InvalidOperationException("Relay Run's definition and quest must belong to its database.");
            if (obstacleViews.Length < DemoRunnerModel.ObstacleCapacity)
                throw new InvalidOperationException("Relay Run needs five authored obstacle views.");
            if (obstacleSprites == null || obstacleSprites.Length != DemoRunnerModel.AppearanceCount)
                throw new InvalidOperationException("Relay Run needs one obstacle sprite per traced obstacle outline.");
            foreach (DialogDef conversation in new[] { briefingDialog, failureDialog, successDialog })
                if (conversation == null || database.Find<DialogDef>(conversation.DefName) != conversation)
                    throw new InvalidOperationException("Relay Run's conversations must belong to its database.");
            runnerTint = runnerImage.color;
            dialogue = DialoguePanel.Create();
            dialogue.Runner.Closed += OnDialogClosed;
            foreach (ScrollLayer layer in scenery) layer.origin = layer.first.anchoredPosition;
            best = ContextManager.Ins.GetGameDataInt(GameRegister.SCOPE_HISCORE);
            bestText.ShowPlain(best.ToString("000"));
            startButton.onClick.AddListener(BeginRun);
            jumpButton.onClick.AddListener(Jump);
            pauseButton.onClick.AddListener(TogglePause);
            resumeButton.onClick.AddListener(TogglePause);
            retryButton.onClick.AddListener(BeginRun);
            homeButton.onClick.AddListener(ReturnToMenu);
            resultHomeButton.onClick.AddListener(ReturnToMenu);
            topHomeButton?.onClick.AddListener(ReturnToMenu);
            readyPanel.SetActive(true);
            pausePanel.SetActive(false);
            resultPanel.SetActive(false);
            foreach (Image obstacle in obstacleViews) obstacle.gameObject.SetActive(false);
            ready = true;
        }

        public void BeginRun()
        {
            if (!ready || dialogue.BlocksInput || UICharacterDialog.BlocksInput) return;
            if (quest != null && !quest.IsFinished) return;
            if (QuestLog.Locked(definition.quest))
            {
                UICharacterDialog.Alert(lockedHeading, lockedBody);
                return;
            }
            DisposeQuest();
            footFx.Clear();
            rexAnimation.ResetPose();
            model = null;
            running = paused = awaitingBriefing = awaitingOutcome = false;
            readyPanel.SetActive(false);
            pausePanel.SetActive(false);
            resultPanel.SetActive(false);
            foreach (Image obstacle in obstacleViews) obstacle.gameObject.SetActive(false);
            quest = new QuestRun(definition.quest);
            quest.Finished += OnFinished;
            quest.Steps.Changed += OnQuestChanged;
            quest.Begin();
        }

        void OnDialogClosed(bool completed)
        {
            if (!ready || !completed) return;
            if (awaitingBriefing)
            {
                awaitingBriefing = false;
                quest.Raise(DemoRunnerSignals.Briefed);
            }
            else if (awaitingOutcome)
            {
                awaitingOutcome = false;
                resultPanel.SetActive(true);
            }
        }

        void OnQuestChanged()
        {
            if (quest.IsFinished) return;
            if (quest.Steps["briefing"].Active && !awaitingBriefing)
            {
                awaitingBriefing = true;
                dialogue.Show(briefingDialog, allowClose: false);
            }
            // The quest's authored prerequisite reveals the activity; a button cannot bypass it.
            if (quest.Steps["delivery"].Active && model == null) StartGameplay();
        }

        void StartGameplay()
        {
            model = new DemoRunnerModel(definition.tuning, UnityEngine.Random.Range(0, int.MaxValue));
            reportedMetres = reportedClears = 0;
            paused = false;
            running = true;
            readyPanel.SetActive(false);
            pausePanel.SetActive(false);
            resultPanel.SetActive(false);
            runnerImage.color = runnerTint;
            Present();
        }

        void Update()
        {
            if (!ready) return;
            if (dialogue.BlocksInput || UICharacterDialog.BlocksInput)
            {
                if (model != null) rexAnimation.Present(false, model.PreparingJump || !model.Grounded, model.Speed);
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame) TogglePause();
                if (keyboard.spaceKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                {
                    if (!running) BeginRun();
                    else Jump();
                }
            }
            if (!running || paused) return;
            int jumps = model.Jumps;
            int preparations = model.JumpPreparations;
            int landings = model.Landings;
            float travelled = model.Travelled;
            float seconds = Mathf.Min(Time.deltaTime, .1f);
            model.Advance(seconds);
            footFx.Advance(seconds, model.Travelled - travelled);
            if (model.JumpPreparations > preparations) rexAnimation.Jump();
            // Counters retain a landing even when a buffered takeoff occurs in this same frame.
            if (model.Landings > landings)
            {
                footFx.Land();
                // A buffered takeoff keeps its own squash.
                if (model.Grounded && !model.PreparingJump) rexAnimation.Land();
            }
            if (model.Jumps > jumps)
            {
                footFx.Jump();
                PlayFeedback(jumpFx, jumpCue);
            }
            // A collision on the finish frame takes precedence over the distance ending.
            if (model.Outcome == DemoRunnerOutcome.Crashed && !quest.IsFinished) quest.Raise(DemoRunnerSignals.Crashed);
            int metres = Mathf.FloorToInt(model.Distance);
            if (metres > reportedMetres)
            {
                SignalBus.Fire(new QuestSignal(DemoRunnerSignals.Metre, quest, metres - reportedMetres));
                reportedMetres = metres;
            }
            if (model.Cleared > reportedClears)
            {
                SignalBus.Fire(new QuestSignal(DemoRunnerSignals.Cleared, quest, model.Cleared - reportedClears));
                reportedClears = model.Cleared;
            }
            Present();
        }

        public void Jump()
        {
            if (!running || paused || dialogue.BlocksInput || UICharacterDialog.BlocksInput) return;
            int preparations = model.JumpPreparations;
            model.Jump();
            if (model.JumpPreparations > preparations) rexAnimation.Jump();
        }

        public void TogglePause()
        {
            if (!running || dialogue.BlocksInput || UICharacterDialog.BlocksInput) return;
            paused = !paused;
            pausePanel.SetActive(paused);
            Present();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && running && !paused) TogglePause();
        }

        void OnFinished(QuestEnding ending)
        {
            footFx.Clear();
            running = false;
            paused = false;
            pausePanel.SetActive(false);
            resultPanel.SetActive(false);
            successHeading.SetActive(ending.Passes);
            failureHeading.SetActive(!ending.Passes);
            int metres = Mathf.FloorToInt(model.Distance);
            resultDistanceText.ShowPlain(metres.ToString("000") + " / " + definition.tuning.goalDistance);
            if (metres > best)
            {
                best = metres;
                ContextManager.Ins.SaveGameDataInt(GameRegister.SCOPE_HISCORE, best);
                bestText.ShowPlain(best.ToString("000"));
            }
            if (!ending.Passes) runnerImage.color = failureTint;
            PlayFeedback(ending.Passes ? successFx : crashFx, ending.Passes ? successCue : crashCue);
            awaitingOutcome = true;
            dialogue.Show(ending.Passes ? successDialog : failureDialog, allowClose: false);
        }

        void PlayFeedback(FxEventDef effect, SoundCueDef fallback)
        {
            if (effect != null) SignalBus.Fire(new FxRequestedSignal { Fx = effect, Position = runner.position });
            else SignalBus.Fire(new SoundRequestedSignal { Cue = fallback });
        }

        void Present()
        {
            distanceText.ShowPlain(Mathf.FloorToInt(model.Distance).ToString("000"));
            clearedText.ShowPlain(model.Cleared.ToString("00"));
            progressFill.fillAmount = model.Distance / definition.tuning.goalDistance;
            runner.anchoredPosition = new Vector2(DemoRunnerModel.RunnerX, DemoRunnerModel.GroundY + model.Height);
            runner.localRotation = Quaternion.Euler(0, 0, model.Grounded ? 0 : Mathf.Clamp(model.Velocity * .012f, -10, 10));
            rexAnimation.Present(running && !paused, model.PreparingJump || !model.Grounded, model.Speed);
            for (int i = 0; i < obstacleViews.Length; i++)
            {
                Image view = obstacleViews[i];
                bool active = i < model.Obstacles.Count && model.Obstacles[i].Active;
                view.gameObject.SetActive(active);
                if (!active) continue;
                DemoRunnerModel.Obstacle obstacle = model.Obstacles[i];
                view.rectTransform.anchoredPosition = new Vector2(obstacle.X, DemoRunnerModel.GroundY);
                view.rectTransform.sizeDelta = new Vector2(definition.tuning.obstacleWidth, definition.tuning.obstacleHeight);
                // Collision uses this sprite's traced outline, so appearances map one-to-one.
                view.sprite = obstacleSprites[obstacle.Appearance];
            }
            foreach (ScrollLayer layer in scenery)
            {
                float offset = Mathf.Repeat(model.Travelled * layer.speedFactor, layer.width);
                layer.first.anchoredPosition = layer.origin + Vector2.left * offset;
                layer.second.anchoredPosition = layer.origin + Vector2.right * (layer.width - offset);
            }
        }

        public void ReturnToMenu()
        {
            if (!ready || confirmingExit || UICharacterDialog.BlocksInput) return;
            if (running)
            {
                bool wasPaused = paused;
                paused = true;
                pausePanel.SetActive(true);
                Present();
                confirmingExit = true;
                UICharacterDialog.Confirm(abandonHeading, abandonBody, accepted =>
                {
                    if (this == null || !ready) return;
                    confirmingExit = false;
                    if (accepted) LeaveScene();
                    else
                    {
                        paused = wasPaused;
                        pausePanel.SetActive(paused);
                        Present();
                    }
                }, abandonAccept);
                return;
            }
            LeaveScene();
        }

        void LeaveScene()
        {
            ready = false;
            running = false;
            awaitingBriefing = false;
            dialogue.Runner.Cancel();
            DisposeQuest();
            ContextManager.Ins.ReturnToIndex();
        }

        void DisposeQuest()
        {
            if (quest == null) return;
            quest.Finished -= OnFinished;
            quest.Steps.Changed -= OnQuestChanged;
            quest.Dispose();
            quest = null;
        }

        void OnDestroy()
        {
            ready = false;
            DisposeQuest();
            if (dialogue != null)
            {
                dialogue.Runner.Closed -= OnDialogClosed;
                Destroy(dialogue.gameObject);
            }
        }
    }
}
