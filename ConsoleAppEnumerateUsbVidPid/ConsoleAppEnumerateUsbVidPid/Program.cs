using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management; // need to add System.Management to your project references. Right click on References in solution explorer -> Add Reference. Find Assemblies\Framework, check System.Management and click ok.
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.IO;

namespace ConsoleAppEnumerateUsbVidPid
{
   
    class Program
    {
        static void Main(string[] args)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Removable)
                {
                    GetDriveVidPid(drive.Name);
                }
            }
            Console.WriteLine("press any key to exit =========");
            Console.ReadKey();
        }
        public static bool GetDriveVidPid(string szDriveName) //this works for composite deivce and some usb sticks which provide serial number to query.
        {
            bool bResult = false;
            string szSerialNumberDevice = null;
            //string szDriveName="E:";
            ushort wVID = 0;
            ushort wPID = 0;


            ManagementObject oLogicalDisk = new ManagementObject("Win32_LogicalDisk.DeviceID='" + szDriveName.TrimEnd('\\') + "'");
            foreach (ManagementObject oDiskPartition in oLogicalDisk.GetRelated("Win32_DiskPartition"))
            {
                foreach (ManagementObject oDiskDrive in oDiskPartition.GetRelated("Win32_DiskDrive"))
                {
                    string szPNPDeviceID = oDiskDrive["PNPDeviceID"].ToString();
                    if (!szPNPDeviceID.StartsWith("USBSTOR"))
                        throw new Exception(szDriveName + " is not a USB drive.");

                    string[] aszToken = szPNPDeviceID.Split(new char[] { '\\', '&' });
                    szSerialNumberDevice = aszToken[aszToken.Length - 2];
                }
            }

            if (null != szSerialNumberDevice)
            {
                ManagementObjectSearcher oSearcher = new ManagementObjectSearcher(@"root\CIMV2", "Select * from Win32_USBHub");
                foreach (ManagementObject oResult in oSearcher.Get())
                {
                    object oValue = oResult["DeviceID"];
                    if (oValue == null)
                        continue;

                    string szDeviceID = oValue.ToString();
                    string[] aszToken = szDeviceID.Split(new char[] { '\\' });
                    if (szSerialNumberDevice != aszToken[aszToken.Length - 1])
                        continue;

                    int nTemp = szDeviceID.IndexOf(@"VID_");
                    if (0 > nTemp)
                        continue;

                    nTemp += 4;
                    wVID = ushort.Parse(szDeviceID.Substring(nTemp, 4), System.Globalization.NumberStyles.AllowHexSpecifier);

                    nTemp += 4;
                    nTemp = szDeviceID.IndexOf(@"PID_", nTemp);
                    if (0 > nTemp)
                        continue;

                    nTemp += 4;
                    wPID = ushort.Parse(szDeviceID.Substring(nTemp, 4), System.Globalization.NumberStyles.AllowHexSpecifier);

                    bResult = true;
                    break;
                }
            }
            if (bResult == true)
            {
                Console.WriteLine("{0} VID={1}, PID={2}", szDriveName, wVID.ToString("X4"), wPID.ToString("X4"));
            }
            return bResult;
        }

        public static void printRelated(ManagementObject mo)
        {
            Console.WriteLine("=======================");
            foreach (ManagementObject b in mo.GetRelated())
                Console.WriteLine(
                    "Object related to Alerter service : {0}",
                    b.ClassPath);
        }
        public static void printPorperty(ManagementObject mo)
        {
            Console.WriteLine("//=====================================");
            foreach (PropertyData data in mo.Properties)
            {
                if (data.Value == null)
                {
                    Console.WriteLine(data.Name + ": ");
                }
                else
                {
                    Console.WriteLine(data.Name + ": " + data.Value.ToString());
                }
            }
            Console.WriteLine("=====================================//");
        }
    }
}
