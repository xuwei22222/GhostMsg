using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace IniFile
{
    public class editini
    {
        public string path;             //INI file

        // API import
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileStringW(
            string section, string key, string def,
            StringBuilder retVal, int size, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern long WritePrivateProfileStringW(
            string section, string key, string val, string filePath);

        // path import
        public editini(string INIPath)
        {
            path = INIPath;
        }

        // write ini file
        // name="Section"(exp. [TypeName] )
        // name="Key" 
        // name="Value"
        public void IniWriteValue(string Section,string Key,string Value)
        {
            WritePrivateProfileStringW(Section, Key, Value, this.path);
        }

        //read ini file
        public string IniReadValue(string Section,string Key)
        {
            StringBuilder temp = new StringBuilder(1024);
            GetPrivateProfileStringW(Section, Key, "", temp, temp.Capacity, this.path);
            return temp.ToString();
        }

        // has inifile:true; hasn't inifile:false
        public bool ExistINIFile()
        {
            return File.Exists(this.path);
        }
    }
}