using System;
using System.Text;

namespace Hypocycloid.Utils
{
    /// <summary>
    /// 字符串工具类
    /// </summary>
    public class StringHelper
    {

        #region 字符串格式化

        /// <summary>    
        /// 对字符串进行裁剪
        /// </summary>    
        public static string Trim(string stringTrim, int maxLength)
        {
            return Trim(stringTrim, maxLength, "...");
        }

        /// <summary>    
        /// 对字符串进行裁剪(区分单字节及双字节字符)    
        /// </summary>    
        /// <param name="rawString">需要裁剪的字符串</param>    
        /// <param name="maxLength">裁剪的长度，按双字节计数</param>    
        /// <param name="appendString">如果进行了裁剪需要附加的字符</param>    
        public static string Trim(string rawString, int maxLength, string appendString)
        {
            if (string.IsNullOrEmpty(rawString) || rawString.Length <= maxLength)
            {
                return rawString;
            }
            else
            {
                int rawStringLength = Encoding.UTF8.GetBytes(rawString).Length;
                if (rawStringLength <= maxLength * 2)
                    return rawString;
            }

            int appendStringLength = Encoding.UTF8.GetBytes(appendString).Length;
            StringBuilder checkedStringBuilder = new StringBuilder();
            int appendedLenth = 0;
            for (int i = 0; i < rawString.Length; i++)
            {
                char _char = rawString[i];
                checkedStringBuilder.Append(_char);

                appendedLenth += Encoding.Default.GetBytes(new char[] { _char }).Length;

                if (appendedLenth >= maxLength * 2 - appendStringLength)
                    break;
            }

            return checkedStringBuilder.ToString() + appendString;
        }


        #endregion

        /// <summary>    
        /// 清除xml中的不合法字符    
        /// </summary>    
        /// <remarks>    
        /// 无效字符：    
        /// 0x00 - 0x08    
        /// 0x0b - 0x0c    
        /// 0x0e - 0x1f    
        /// </remarks>    
        public static string CleanInvalidCharsForXML(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            else
            {
                StringBuilder checkedStringBuilder = new StringBuilder();
                char[] chars = input.ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    int charValue = Convert.ToInt32(chars[i]);

                    if ((charValue >= 0x00 && charValue <= 0x08) || (charValue >= 0x0b && charValue <= 0x0c) || (charValue >= 0x0e && charValue <= 0x1f))
                        continue;
                    else
                        checkedStringBuilder.Append(chars[i]);
                }

                return checkedStringBuilder.ToString();

                //string result = checkedStringBuilder.ToString();    
                //result = result.Replace("�", "");    
                //return Regex.Replace(result, @"[\u0000-\u0008\u000B\u000C\u000E-\u001A\uD800-\uDFFF]", delegate(Match m) { int code = (int)m.Value.ToCharArray()[0]; return (code > 9 ? "&#" + code.ToString() : "�" + code.ToString()) + ";"; });    
            }
        }

        /// <summary>    
        /// 去掉最后一个逗号    
        /// </summary>    
        /// <param name="String">要做处理的字符串</param>    
        /// <returns>去掉最后一个逗号的字符串</returns>    
        public static string DelLastComma(string String)
        {
            if (String.IndexOf(",") == -1)
            {
                return String;
            }
            return String.Substring(0, String.LastIndexOf(","));
        }

        /// <summary>    
        /// 删除最后一个字符    
        /// </summary>    
        /// <param name="str"></param>    
        /// <returns></returns>    
        public static string ClearLastChar(string str)
        {
            return (str == "") ? "" : str[..^1];
        }

        /// <summary>    
        /// html编码    
        /// </summary>    
        /// <param name="chr"></param>    
        /// <returns></returns>    
        public static string Html_text(string chr)
        {
            if (chr == null)
                return "";
            chr = chr.Replace("'", "''");
            chr = chr.Replace("<", "<");
            chr = chr.Replace(">", ">");
            return (chr);
        }

        /// <summary>    
        /// html解码    
        /// </summary>    
        /// <param name="chr"></param>    
        /// <returns></returns>    
        public static string Text_html(string chr)
        {
            if (chr == null)
                return "";
            chr = chr.Replace("<", "<");
            chr = chr.Replace(">", ">");
            return (chr);
        }

        public static bool JustifyStr(string strValue)
        {
            bool flag = false;
            char[] str = "^<>'=&*, ".ToCharArray(0, 8);
            for (int i = 0; i < 8; i++)
            {
                if (strValue.IndexOf(str[i]) != -1)
                {
                    flag = true;
                    break;
                }
            }
            return flag;
        }

        public static string CheckOutputString(string key)
        {
            string OutputString = string.Empty;
            OutputString = key.Replace("<br>", "\n").Replace("<", "<").Replace(">", ">").Replace(" ", " ");
            return OutputString;

        }

    }
}