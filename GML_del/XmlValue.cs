using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GML_del
{
    public static class XmlValue
    {
        public static string value;
        public static bool template = false;
        public static bool error = false;

        public static void GetValue(string txt)
        {

            txt = txt.Trim();
            if (txt.Substring(txt.Length - 2, 2) == "/>")                       // wartość specjalna
            {
                if (txt.Contains("xsi:nil"))
                {
                    value = "";
                    template = true;
                    error = false;
                    return;
                }
            }
            try
            {
                int i = txt.IndexOf('>');
                value = txt.Substring(i + 1, txt.Length - (2 * i + 3));
                template = false;
                error = false;
            }
            catch (Exception e)
            {
                value = e.Message;
                template = false;
                error = true;
                return;
            }


        }

    }
}
