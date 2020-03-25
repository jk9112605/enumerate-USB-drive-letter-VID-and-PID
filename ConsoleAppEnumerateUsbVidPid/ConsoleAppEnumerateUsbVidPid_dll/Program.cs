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
        public static List<string> USBobjects = new List<string>();
        public static string Secondentity = null;
        private static string USB_PID;
        private static string USB_VID;

        static void Main(string[] args)
        {
            //Get a list of available devices attached to the USB hub From Win32_USBHub
            List<string> disks = new List<string>();
            var usbDevices = GetUSBDevices();

            //for each USB devices to see if any have VID/PID keywords
            foreach (var usbDevice in usbDevices)
            {
                // Define a regular expression for PID VID.
                Regex rx = new Regex("^USB\\\\VID_(?<vid>\\w{4})\\&PID_(?<pid>\\w{4}).*$");
                // Find matches.
                MatchCollection matches = rx.Matches(usbDevice.DeviceID);
                if (matches.Count > 0)
                {
                    USB_VID = matches[0].Groups["vid"].Value;
                    USB_PID = matches[0].Groups["pid"].Value;
                    //Console.WriteLine("VID={0}, PID={1}", matches[0].Groups["vid"].Value, matches[0].Groups["pid"].Value);
                    {
                        foreach (string name in usbDevice.GetDiskNames())
                        {
                            //Open dialog to show file names
                            Console.WriteLine("PID={0}, VID={1}", USB_PID, USB_VID);
                            Console.WriteLine(name.ToString());
                        }
                    }
                }
            }
            Console.WriteLine("press any key to exit =========");
            Console.ReadKey();
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
        
        /// <summary>
        /// Get a list of available devices attached to the USB hub From Win32_USBHub
        /// </summary>
        /// <returns></returns>
        public static List<USBDeviceInfo> GetUSBDevices()
        {
            List<USBDeviceInfo> devices = new List<USBDeviceInfo>();
            
            ManagementObjectCollection collection;
            using (var searcher = new ManagementObjectSearcher(@"Select * From Win32_USBHub"))
                collection = searcher.Get();

            foreach (var device in collection)
            {
                devices.Add(new USBDeviceInfo(
                (string)device.GetPropertyValue("DeviceID"),
                (string)device.GetPropertyValue("PNPDeviceID"),
                (string)device.GetPropertyValue("Description")
                ));
            }

            collection.Dispose();
            return devices;
        }
    }
    class USBDeviceInfo
    {
        public USBDeviceInfo(string deviceID, string pnpDeviceID, string description)
        {
            this.DeviceID = deviceID;
            this.PnpDeviceID = pnpDeviceID;
            this.Description = description;
        }

        public string DeviceID { get; private set; }
        public string PnpDeviceID { get; private set; }
        public string Description { get; private set; }

        public IEnumerable<string> GetDiskNames()
        {
            using (Device device = Device.Get(PnpDeviceID))
            {
                if (device != null)
                {
                    // get children devices
                    foreach (string childDeviceId in device.ChildrenPnpDeviceIds)
                    {
                        // get the drive object that correspond to this id (escape the id)
                        foreach (ManagementObject drive in new ManagementObjectSearcher("SELECT DeviceID FROM Win32_DiskDrive WHERE PNPDeviceID='" + childDeviceId.Replace(@"\", @"\\") + "'").Get())
                        {
                            // associate physical disks with partitions
                            foreach (ManagementObject partition in new ManagementObjectSearcher("ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + drive["DeviceID"] + "'} WHERE AssocClass=Win32_DiskDriveToDiskPartition").Get())
                            {
                                // associate partitions with logical disks (drive letter volumes)
                                foreach (ManagementObject disk in new ManagementObjectSearcher("ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + partition["DeviceID"] + "'} WHERE AssocClass=Win32_LogicalDiskToPartition").Get())
                                {
                                    yield return (string)disk["DeviceID"];
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    public sealed class Device : IDisposable
    {
        private IntPtr _hDevInfo;
        private SP_DEVINFO_DATA _data;

        private Device(IntPtr hDevInfo, SP_DEVINFO_DATA data)
        {
            _hDevInfo = hDevInfo;
            _data = data;
        }
        public static Device Get(string pnpDeviceId)
        {
            if (pnpDeviceId == null)
                throw new ArgumentNullException("pnpDeviceId");
            IntPtr hDevInfo = SetupDiGetClassDevs(IntPtr.Zero, pnpDeviceId, IntPtr.Zero, DIGCF.DIGCF_ALLCLASSES | DIGCF.DIGCF_DEVICEINTERFACE);
            if (hDevInfo == (IntPtr)INVALID_HANDLE_VALUE)
                throw new Exception(Marshal.GetLastWin32Error().ToString());

            SP_DEVINFO_DATA data = new SP_DEVINFO_DATA();
            data.cbSize = Marshal.SizeOf(data);
            ManagementObjectCollection col1 = new ManagementObjectSearcher("Select * From Win32_PnPEntity Where PnPDeviceID Like '%VID_C251&PID_2750%'").Get(); //Not case sensitive
            foreach (ManagementObject o1 in col1)
            {
                if(o1["Name"].ToString().Contains("USB Mass Storage"))
                {
                    data.ClassGuid = new Guid(o1["ClassGuid"].ToString());
                    data.DevInst = 1;
                    return new Device(hDevInfo, data) { PnpDeviceId = pnpDeviceId };
                }
            }
                


            if (!SetupDiEnumDeviceInfo(hDevInfo, 0, ref data))
            {
                int err = Marshal.GetLastWin32Error();
                if (err == ERROR_NO_MORE_ITEMS)
                    return null;

                throw new Exception(err.ToString());
            }

            return new Device(hDevInfo, data) { PnpDeviceId = pnpDeviceId };
        }

        public static Device Get1(string pnpDeviceId)
        {
            if (pnpDeviceId == null)
                throw new ArgumentNullException("pnpDeviceId");

            IntPtr hDevInfo = SetupDiGetClassDevs(IntPtr.Zero, pnpDeviceId, IntPtr.Zero, DIGCF.DIGCF_ALLCLASSES | DIGCF.DIGCF_DEVICEINTERFACE);
            if (hDevInfo == (IntPtr)INVALID_HANDLE_VALUE)
                throw new Exception(Marshal.GetLastWin32Error().ToString());

            SP_DEVINFO_DATA data = new SP_DEVINFO_DATA();
            data.cbSize = Marshal.SizeOf(data);
            if (!SetupDiEnumDeviceInfo(hDevInfo, 0, ref data))
            {
                int err = Marshal.GetLastWin32Error();
                if (err == ERROR_NO_MORE_ITEMS)
                    return null;

                throw new Exception(err.ToString());
            }

            return new Device(hDevInfo, data) { PnpDeviceId = pnpDeviceId };
        }
        public void Dispose()
        {
            if (_hDevInfo != IntPtr.Zero)
            {
                SetupDiDestroyDeviceInfoList(_hDevInfo);
                _hDevInfo = IntPtr.Zero;
            }
        }

        public string PnpDeviceId { get; private set; }

        public string ParentPnpDeviceId
        {
            get
            {
                if (IsVistaOrHiger)
                    return GetStringProperty(DEVPROPKEY.DEVPKEY_Device_Parent);

                uint parent;
                int cr = CM_Get_Parent(out parent, _data.DevInst, 0);
                if (cr != 0)
                    throw new Exception("CM Error:" + cr);

                return GetDeviceId(parent);
            }
        }

        private static string GetDeviceId(uint inst)
        {
            IntPtr buffer = Marshal.AllocHGlobal(MAX_DEVICE_ID_LEN + 1);
            int cr = CM_Get_Device_ID(inst, buffer, MAX_DEVICE_ID_LEN + 1, 0);
            if (cr != 0)
                throw new Exception("CM Error:" + cr);

            try
            {
                return Marshal.PtrToStringAnsi(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public string[] ChildrenPnpDeviceIds
        {
            get
            {
                if (IsVistaOrHiger)
                    return GetStringListProperty(DEVPROPKEY.DEVPKEY_Device_Children);

                uint child;
                int cr = CM_Get_Child(out child, _data.DevInst, 0);
                if (cr != 0)
                    return new string[0];

                List<string> ids = new List<string>();
                ids.Add(GetDeviceId(child));
                do
                {
                    cr = CM_Get_Sibling(out child, child, 0);
                    if (cr != 0)
                        return ids.ToArray();

                    ids.Add(GetDeviceId(child));
                }
                while (true);
            }
        }


        private static bool IsVistaOrHiger
        {
            get
            {
                return (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.CompareTo(new Version(6, 0)) >= 0);
            }
        }

        private const int INVALID_HANDLE_VALUE = -1;
        private const int ERROR_NO_MORE_ITEMS = 259;
        private const int MAX_DEVICE_ID_LEN = 200;

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [Flags]
        private enum DIGCF : uint
        {
            DIGCF_DEFAULT = 0x00000001,
            DIGCF_PRESENT = 0x00000002,
            DIGCF_ALLCLASSES = 0x00000004,
            DIGCF_PROFILE = 0x00000008,
            DIGCF_DEVICEINTERFACE = 0x00000010,
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(IntPtr ClassGuid, string Enumerator, IntPtr hwndParent, DIGCF Flags);

        [DllImport("setupapi.dll")]
        private static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        [DllImport("setupapi.dll")]
        private static extern int CM_Get_Device_ID(uint dnDevInst, IntPtr Buffer, int BufferLen, uint ulFlags);

        [DllImport("setupapi.dll")]
        private static extern int CM_Get_Child(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        [DllImport("setupapi.dll")]
        private static extern int CM_Get_Sibling(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        [DllImport("setupapi.dll")]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        // vista and higher
        [DllImport("setupapi.dll", SetLastError = true, EntryPoint = "SetupDiGetDevicePropertyW")]
        private static extern bool SetupDiGetDeviceProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref DEVPROPKEY propertyKey, out int propertyType, IntPtr propertyBuffer, int propertyBufferSize, out int requiredSize, int flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;

            // from devpkey.h
            public static readonly DEVPROPKEY DEVPKEY_Device_Parent = new DEVPROPKEY { fmtid = new Guid("{4340A6C5-93FA-4706-972C-7B648008A5A7}"), pid = 8 };
            public static readonly DEVPROPKEY DEVPKEY_Device_Children = new DEVPROPKEY { fmtid = new Guid("{4340A6C5-93FA-4706-972C-7B648008A5A7}"), pid = 9 };
        }

        private string[] GetStringListProperty(DEVPROPKEY key)
        {
            int type;
            int size;
            SetupDiGetDeviceProperty(_hDevInfo, ref _data, ref key, out type, IntPtr.Zero, 0, out size, 0);
            if (size == 0)
                return new string[0];

            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!SetupDiGetDeviceProperty(_hDevInfo, ref _data, ref key, out type, buffer, size, out size, 0))
                    throw new Exception(Marshal.GetLastWin32Error().ToString());

                List<string> strings = new List<string>();
                IntPtr current = buffer;
                do
                {
                    string s = Marshal.PtrToStringUni(current);
                    if (string.IsNullOrEmpty(s))
                        break;

                    strings.Add(s);
                    current += (1 + s.Length) * 2;
                }
                while (true);
                return strings.ToArray();
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private string GetStringProperty(DEVPROPKEY key)
        {
            int type;
            int size;
            SetupDiGetDeviceProperty(_hDevInfo, ref _data, ref key, out type, IntPtr.Zero, 0, out size, 0);
            if (size == 0)
                return null;

            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!SetupDiGetDeviceProperty(_hDevInfo, ref _data, ref key, out type, buffer, size, out size, 0))
                    throw new Exception(Marshal.GetLastWin32Error().ToString());

                return Marshal.PtrToStringUni(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }


}
