/* * * * * * * * * * * * * * * * * * * * * * * * * * *
 * === Notes ===
 * 
 * - When using the Toshiba Stack,
 *   Use WriteFile with 22 byte reports
 *   
 * - When On Windows 8 & 10 with MS Stack,
 *   Use WriteFile with minimum report size
 *   
 * - When On Windows 7 or lower with MS Stack,
 *   Use SetOutputReport (does not work with TR/Pro)
 *   
 * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using static Shared.Windows.NativeImports;
using NintrollerLib;
using Shared;

namespace Shared.Windows
{
    public class WinBtStream : Stream
    {
        #region Members
        public static bool OverrideSharingMode = false;
        public static FileShare OverridenFileShare = FileShare.None;
        public static bool ForceToshibaMode = false;

        protected string _hidPath = null!;
        protected SafeFileHandle? _fileHandle;
        protected FileStream? _fileStream;
        protected object _writerBlock = null!;
        #endregion

        #region Properties
        /// <summary>
        /// Set to None to have exclusive access to the controller.
        /// Otherwise set to ReadWrite.
        /// </summary>
        public FileShare SharingMode { get; set; } = FileShare.ReadWrite;

        /// <summary>
        /// Set if the user is using the Toshiba Bluetooth Stack
        /// </summary>
        public static bool UseToshiba { get; set; }

        /// <summary>
        /// Set to use the WriteFile method (allows use with the Microsoft Bluetooth Stack)
        /// </summary>
        public bool UseWriteFile { get; set; }

        /// <summary>
        /// Set when using to use 22 byte reports when sending data (use with Toshiba Stack or Set_Output_Report)
        /// </summary>
        public bool UseFullReportSize { get; set; }
        #endregion

        public enum BtStack
        {
            Microsoft,
            Toshiba,
            Other
        }

        static WinBtStream()
        {
            // When true, Windows Stack is enabled
            //var a = BluetoothEnableDiscovery(IntPtr.Zero, true);
        }

        public WinBtStream(string path)
        {
            UseToshiba = ForceToshibaMode;// || !BluetoothEnableDiscovery(IntPtr.Zero, true);

            // Default Windows 8/10 to ReadWrite (non exclusive)
            if (Environment.OSVersion.Version.Major > 6 
                || (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor == 2)) // temp
            {
                SharingMode = FileShare.ReadWrite;
                UseWriteFile = true;

                // A certian build of Windows 10 seems to have fixed the FileShare.None issue
                //if (Environment.OSVersion.Version.Major == 10 &&
                //    Environment.OSVersion.Version.Build >= 10586/* &&
                //    Environment.OSVersion.Version.Build < 14393*/)
                //{
                //    SharingMode = FileShare.None;
                //}
            }
            else
            {
                SharingMode = FileShare.None;
                UseFullReportSize = true;
            }

            // Determine if using the Toshiba Stack
            if (UseToshiba)
            {
                SharingMode = FileShare.None;
                UseFullReportSize = true;
                UseWriteFile = true;
            }
            
            _hidPath = path;
            _writerBlock = new object();
        }

        public WinBtStream(string path, BtStack btStack) : this(path)
        {
            if (btStack == BtStack.Toshiba)
            {
                UseFullReportSize = true;
                UseWriteFile = true;
            }
        }

        public WinBtStream(string path, BtStack btStack, FileShare sharingMode) : this(path, btStack)
        {
            SharingMode = sharingMode;
        }

        public bool OpenConnection()
        {
            var result = TryOpenConnection();
            return result.IsOk;
        }

        public Result<WinBtStream, HidStreamError> TryOpenConnection()
        {
            if (string.IsNullOrWhiteSpace(_hidPath))
                return Result<WinBtStream, HidStreamError>.Err(HidStreamError.InvalidPath("Device path is empty.", _hidPath));

            try
            {
                if (OverrideSharingMode)
                {
                    _fileHandle = CreateFile(_hidPath, FileAccess.ReadWrite, OverridenFileShare, IntPtr.Zero, FileMode.Open, EFileAttributes.Overlapped, IntPtr.Zero);
                }
                else
                {
                    // Open the file handle with the specified sharing mode and an overlapped file attribute flag for asynchronous operation
                    _fileHandle = CreateFile(_hidPath, FileAccess.ReadWrite, SharingMode, IntPtr.Zero, FileMode.Open, EFileAttributes.Overlapped, IntPtr.Zero);
                }
                _fileStream = new FileStream(_fileHandle, FileAccess.ReadWrite, 22, true);

                return Result<WinBtStream, HidStreamError>.Ok(this);
            }
            catch (UnauthorizedAccessException ex)
            {
                _fileHandle = null;
                return Result<WinBtStream, HidStreamError>.Err(HidStreamError.AccessDenied("Access denied opening HID stream.", _hidPath, ex));
            }
            catch (IOException ex)
            {
                _fileHandle = null;
                if (SharingMode == FileShare.None)
                {
                    SharingMode = FileShare.ReadWrite;
                    return TryOpenConnection();
                }

                return Result<WinBtStream, HidStreamError>.Err(HidStreamError.OpenFailed("I/O failure opening HID stream.", _hidPath, ex));
            }
            catch (Exception ex)
            {
                _fileHandle = null;
                if (SharingMode == FileShare.None)
                {
                    SharingMode = FileShare.ReadWrite;
                    return TryOpenConnection();
                }

                return Result<WinBtStream, HidStreamError>.Err(HidStreamError.Unknown("Unexpected failure opening HID stream.", _hidPath, ex));
            }
        }

        public Result<Unit, HidStreamError> TryWrite(byte[] buffer, int offset, int count)
        {
            if (_fileStream == null)
                return Result<Unit, HidStreamError>.Err(HidStreamError.DeviceDisappeared("Stream is not open.", _hidPath));

            try
            {
                _fileStream.Write(buffer, offset, count);
                return Result<Unit, HidStreamError>.Ok(Unit.Value);
            }
            catch (IOException ex)
            {
                return Result<Unit, HidStreamError>.Err(HidStreamError.WriteFailed("Failed writing HID report.", _hidPath, ex));
            }
            catch (ObjectDisposedException ex)
            {
                return Result<Unit, HidStreamError>.Err(HidStreamError.DeviceDisappeared("Stream was disposed while writing.", _hidPath, ex));
            }
            catch (Exception ex)
            {
                return Result<Unit, HidStreamError>.Err(HidStreamError.Unknown("Unexpected HID write failure.", _hidPath, ex));
            }
        }

        public Result<int, HidStreamError> TryRead(byte[] buffer, int offset, int count)
        {
            if (_fileStream == null)
                return Result<int, HidStreamError>.Err(HidStreamError.DeviceDisappeared("Stream is not open.", _hidPath));

            try
            {
                return Result<int, HidStreamError>.Ok(_fileStream.Read(buffer, offset, count));
            }
            catch (IOException ex)
            {
                return Result<int, HidStreamError>.Err(HidStreamError.ReadFailed("Failed reading HID report.", _hidPath, ex));
            }
            catch (ObjectDisposedException ex)
            {
                return Result<int, HidStreamError>.Err(HidStreamError.DeviceDisappeared("Stream was disposed while reading.", _hidPath, ex));
            }
            catch (Exception ex)
            {
                return Result<int, HidStreamError>.Err(HidStreamError.Unknown("Unexpected HID read failure.", _hidPath, ex));
            }
        }

        public static BtStack CheckBtStack(SP_DEVINFO_DATA data)
        {
            // Assume it is the Microsoft Stack
            BtStack resultStack = BtStack.Microsoft;
            IntPtr parentDeviceInfo = IntPtr.Zero;
            SP_DEVINFO_DATA parentData = new SP_DEVINFO_DATA();
            parentData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));

            int status = 0;
            int problemNum = 0;

            var result = CM_Get_DevNode_Status(ref status, ref problemNum, (int)data.DevInst, 0);

            if (result != 0) return resultStack; // Failed

            uint parentDevice;

            result = CM_Get_Parent(out parentDevice, data.DevInst, 0);

            if (result != 0) return resultStack; // Failed

            char[] parentId = new char[200];

            result = CM_Get_Device_ID(parentDevice, parentId, 200, 0);

            if (result != 0) return resultStack; // Failed

            string id = new string(parentId).Replace("\0", "");

            Guid g = Guid.Empty;
            HidD_GetHidGuid(out g);
            parentDeviceInfo = SetupDiCreateDeviceInfoList(ref g, IntPtr.Zero);

            // TODO: This fails, something not right
            bool success = SetupDiOpenDeviceInfo(parentDeviceInfo, id, IntPtr.Zero, 0, ref parentData);

            if (success)
            {
                int requiredSize = 0;
                ulong devicePropertyType;

                DEVPROPKEY requestedKey = new DEVPROPKEY();
                requestedKey.fmtid = new Guid(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6);
                requestedKey.pid = 9;

                SetupDiGetDeviceProperty(parentDeviceInfo, parentData, requestedKey, out devicePropertyType, null, 0, out requiredSize, 0);

                char[] buffer = new char[requiredSize];
                success = SetupDiGetDeviceProperty(parentDeviceInfo, parentData, requestedKey, out devicePropertyType, buffer, requiredSize, out requiredSize, 0);

                if (success)
                {
                    string classProvider = new string(buffer);
                    classProvider = classProvider.Replace("\0", "");
                    if (classProvider == "TOSHIBA")
                    {
                        // Toshiba Stack
                        resultStack = BtStack.Toshiba;
                    }
                }

                SetupDiDestroyDeviceInfoList(parentDeviceInfo);
            }
            else
            {
                var error = GetLastError();
                SetupDiDestroyDeviceInfoList(parentDeviceInfo);
            }

            return resultStack;
        }

        public static List<DeviceInfo> GetPaths()
        {
            var result = new List<DeviceInfo>();
            Guid guid;
            int index = 0;
            SafeFileHandle handle;

            // Get GUID of the HID class
            HidD_GetHidGuid(out guid);

            // handle for HID devices
            IntPtr hDevInfo = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, (uint)(DIGCF.DeviceInterface | DIGCF.Present));

            SP_DEVICE_INTERFACE_DATA diData = new SP_DEVICE_INTERFACE_DATA();
            diData.cbSize = Marshal.SizeOf(diData);

            // Step through all devices
            while (SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref guid, index, ref diData))
            {
                uint size;

                // Get Device Buffer Size
                SetupDiGetDeviceInterfaceDetail(hDevInfo, ref diData, IntPtr.Zero, 0, out size, IntPtr.Zero);

                // Create Detail Struct
                SP_DEVICE_INTERFACE_DETAIL_DATA diDetail = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                diDetail.size = (uint)(IntPtr.Size == 8 ? 8 : 5);// 4 + Marshal.SystemDefaultCharSize);

                SP_DEVINFO_DATA deviceInfoData = new SP_DEVINFO_DATA();
                deviceInfoData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));

                // Populate Detail Struct
                if (SetupDiGetDeviceInterfaceDetail(hDevInfo, ref diData, ref diDetail, size, out size, ref deviceInfoData))
                {
                    // Open read/write handle
                    handle = CreateFile(diDetail.devicePath, FileAccess.ReadWrite, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, EFileAttributes.Overlapped, IntPtr.Zero);

                    // Create Attributes Structure
                    HIDD_ATTRIBUTES attrib = new HIDD_ATTRIBUTES();
                    attrib.Size = Marshal.SizeOf(attrib);

                    // Populate Attributes
                    if (HidD_GetAttributes(handle.DangerousGetHandle(), ref attrib))
                    {
                        // Check if this is a compatable device
                        if (attrib.VendorID == 0x057e && (attrib.ProductID == 0x0306 || attrib.ProductID == 0x0330))
                        {
                            // TODO: Debug
                            //var associatedStack = CheckBtStack(deviceInfoData);
                            //var associatedStack = BtStack.Microsoft;

                            //var associatedStack = BluetoothEnableDiscovery(IntPtr.Zero, true) ? BtStack.Microsoft : BtStack.Toshiba;
                            //
                            //if (!AssociatedStack.ContainsKey(diDetail.devicePath))
                            //{
                            //    AssociatedStack.Add(diDetail.devicePath, associatedStack);
                            //}

                            result.Add(new DeviceInfo
                            {
                                DevicePath = diDetail.devicePath,
                                Type = attrib.ProductID == 0x0330 ? ControllerType.ProController : ControllerType.Wiimote
                            });
                        }
                    }

                    handle.Close();
                }
                else
                {
                    // Failed
                }

                index += 1;
            }

            // Clean Up
            SetupDiDestroyDeviceInfoList(hDevInfo);

            return result;
        }
        

        #region System.IO.Stream Properties
        public override bool CanRead { get { return _fileStream?.CanRead ?? false; } }

        public override bool CanWrite { get { return _fileStream?.CanWrite ?? false; } }

        public override bool CanSeek { get { return _fileStream?.CanSeek ?? false; } }

        public override long Length { get { return _fileStream?.Length ?? 0; } }

        public override long Position
        {
            get
            {
                return _fileStream?.Position ?? 0;
            }

            set
            {
                if (_fileStream != null)
                    _fileStream.Position = value;
            }
        }
        #endregion

        #region System.IO.Stream Methods
        public override void Close()
        {
            _fileStream?.Close();
            _fileHandle?.Close();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            if (_fileStream == null)
                throw new ObjectDisposedException(nameof(WinBtStream));
            return _fileStream.BeginRead(buffer, 0, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            // TODO: Handle device not connected
            return _fileStream?.EndRead(asyncResult) ?? -1;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            System.Diagnostics.Debug.WriteLine("Writing: " + BitConverter.ToString(buffer));

            if (UseFullReportSize || UseWriteFile)
            {
                var buf = new byte[22];
                buffer.CopyTo(buf, 0);
                buffer = buf;
            }

            lock (_writerBlock)
            {
                var writeResult = TryWrite(buffer, 0, buffer.Length);
                if (writeResult.IsError)
                    throw new IOException(writeResult.Error.Message, writeResult.Error.Exception);
            }
        }

        public override void WriteByte(byte value)
        {
            System.Diagnostics.Debug.WriteLine("Writing single byte");
            throw new NotSupportedException();
        }

        public override void Flush()
        {
            if (_fileStream == null)
                throw new ObjectDisposedException(nameof(WinBtStream));

            _fileStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (_fileStream == null)
                throw new ObjectDisposedException(nameof(WinBtStream));

            return _fileStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            if (_fileStream == null)
                throw new ObjectDisposedException(nameof(WinBtStream));

            _fileStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var readResult = TryRead(buffer, offset, count);
            if (readResult.IsOk)
                return readResult.Value;

            throw new IOException(readResult.Error.Message, readResult.Error.Exception);
        }
        #endregion
    }
}
