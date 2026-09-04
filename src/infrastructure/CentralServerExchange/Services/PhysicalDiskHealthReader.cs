using System.Runtime.InteropServices;
using System.Text;
using Domain.Agent;
using Domain.Agent.Dto;
using Microsoft.Win32.SafeHandles;

namespace CentralServerExchange.Services;

public static class PhysicalDiskHealthReader
{
    public static IReadOnlyList<PhysicalDiskHealth> List(string mainGdbPath)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return ReadAll(mainGdbPath);

            return [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static IReadOnlyList<PhysicalDiskHealth> ReadAll(string mainGdbPath)
    {
        var osLetter = DriveMetricsReader.WindowsSystemDrive().Letter;
        var dbLetter = DriveMetricsReader.FromPath(mainGdbPath).Letter;
        var volumesByDevice = VolumesByDevice();
        var disks = new List<PhysicalDiskHealth>();

        // Только диски с томами: без буквы на сервер не уходит.
        for (var deviceNumber = 0; deviceNumber < 32; deviceNumber++)
        {
            if (!volumesByDevice.TryGetValue(deviceNumber, out var volumes) || volumes.Count == 0)
                continue;

            var disk = new PhysicalDiskHealth
            {
                Partitions = volumes.Select(ToPartition).ToList()
            };
            PhysicalDiskRoles.Apply(disk.Partitions, osLetter, dbLetter);

            using var handle = Native.OpenPhysicalDrive(deviceNumber);
            if (!handle.IsInvalid)
                FillSmart(disk, handle);

            disks.Add(disk);
        }

        return disks;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static Dictionary<int, List<DriveMetrics>> VolumesByDevice()
    {
        var groups = new Dictionary<int, List<DriveMetrics>>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                continue;

            var metrics = DriveMetricsReader.FromPath(drive.Name);
            if (metrics.Letter.Length == 0)
                continue;

            var deviceNumber = DeviceNumber(metrics.Letter);
            if (deviceNumber < 0)
                continue;

            if (!groups.TryGetValue(deviceNumber, out var volumes))
            {
                volumes = [];
                groups[deviceNumber] = volumes;
            }

            volumes.Add(metrics);
        }

        return groups;
    }

    private static DiskPartition ToPartition(DriveMetrics volume) =>
        new()
        {
            Letter = volume.Letter,
            Name = volume.Name,
            Size = volume.TotalBytes,
            FreeSpace = volume.FreeBytes
        };

    private static void FillSmart(PhysicalDiskHealth disk, SafeFileHandle handle)
    {
        var descriptor = QueryProperty(handle, Native.StorageDeviceProperty, 4096);
        disk.Kind = Kind(handle, BusType(descriptor));

        var model = Model(descriptor);
        if (model.Length == 0)
            model = ReadNvmeModel(handle);
        if (model.Length > 0)
            disk.Name = model;

        var nvme = ReadNvmeHealth(handle);
        if (nvme is not null)
        {
            disk.Kind = DiskKind.Nvme;
            Apply(disk, DiskSmartParser.FromNvme(nvme));
            return;
        }

        var ata = ReadAtaSmart(handle);
        if (ata is not null)
            Apply(disk, DiskSmartParser.FromAta(ata, disk.Kind == DiskKind.Hdd ? DiskKind.Hdd : DiskKind.Ssd));
    }

    private static void Apply(PhysicalDiskHealth disk, DiskSmartFacts facts)
    {
        disk.LifePercent = facts.LifePercent;
        disk.BadBlocks = facts.BadBlocks;
        disk.PowerOnDays = facts.PowerOnDays;
    }

    private static int BusType(byte[]? descriptor)
    {
        if (descriptor is null || descriptor.Length < 32)
            return 0;

        var value = BitConverter.ToInt32(descriptor, 28);
        return value is >= 0 and <= 127 ? value : descriptor[28];
    }

    private static string Kind(SafeFileHandle handle, int busType)
    {
        if (busType == Native.BusTypeNvme)
            return DiskKind.Nvme;

        var seek = QueryProperty(handle, Native.StorageDeviceSeekPenaltyProperty, 32);
        if (seek is { Length: >= 12 } && BitConverter.ToUInt32(seek, 4) >= 9)
            return seek[8] != 0 ? DiskKind.Hdd : DiskKind.Ssd;

        var trim = QueryProperty(handle, Native.StorageDeviceTrimProperty, 32);
        if (trim is { Length: >= 12 } && trim[8] != 0)
            return DiskKind.Ssd;

        return string.Empty;
    }

    private static string Model(byte[]? descriptor)
    {
        if (descriptor is null || descriptor.Length < 20)
            return string.Empty;

        var vendor = Ascii(descriptor, BitConverter.ToInt32(descriptor, 12));
        var product = Ascii(descriptor, BitConverter.ToInt32(descriptor, 16));
        return $"{vendor} {product}".Trim();
    }

    private static string Ascii(byte[] buffer, int offset)
    {
        if (offset <= 0 || offset >= buffer.Length)
            return string.Empty;

        var end = offset;
        while (end < buffer.Length && buffer[end] != 0)
            end++;

        return Encoding.ASCII.GetString(buffer, offset, end - offset).Trim();
    }

    private static int DeviceNumber(string letter)
    {
        using var handle = Native.OpenVolume(letter);
        if (handle.IsInvalid)
            return -1;

        var size = Marshal.SizeOf<Native.StorageDeviceNumber>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!Native.DeviceIoControl(handle, Native.IoctlStorageGetDeviceNumber, 0, 0, buffer, (uint)size, out _, 0))
                return -1;

            return Marshal.PtrToStructure<Native.StorageDeviceNumber>(buffer).DeviceNumber;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static byte[]? QueryProperty(SafeFileHandle handle, int propertyId, int outputSize)
    {
        var query = new Native.StoragePropertyQuery { PropertyId = propertyId };
        var inSize = Marshal.SizeOf<Native.StoragePropertyQuery>();
        var inPtr = Marshal.AllocHGlobal(inSize);
        var outPtr = Marshal.AllocHGlobal(outputSize);
        try
        {
            Native.Zero(inPtr, inSize);
            Native.Zero(outPtr, outputSize);
            Marshal.StructureToPtr(query, inPtr, false);
            if (!Native.DeviceIoControl(
                    handle,
                    Native.IoctlStorageQueryProperty,
                    inPtr,
                    (uint)inSize,
                    outPtr,
                    (uint)outputSize,
                    out var returned,
                    0) || returned == 0 || returned > outputSize)
                return null;

            var result = new byte[returned];
            Marshal.Copy(outPtr, result, 0, (int)returned);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(inPtr);
            Marshal.FreeHGlobal(outPtr);
        }
    }

    private static byte[]? ReadNvmeHealth(SafeFileHandle handle)
    {
        const int healthSize = 512;
        var bufferSize = 8 + 40 + healthSize + 256;
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            int[] properties =
            [
                Native.StorageDeviceProtocolSpecificProperty,
                Native.StorageAdapterProtocolSpecificProperty
            ];
            uint[] subValues = [0xFFFFFFFF, 0];

            foreach (var propertyId in properties)
            foreach (var subValue in subValues)
            {
                if (!QueryNvmeHealth(handle, buffer, bufferSize, propertyId, subValue))
                    continue;

                var protocolOffset = Marshal.ReadInt32(buffer, 8 + 16);
                var dataOffset = 8 + protocolOffset;
                if (dataOffset < 0 || dataOffset + healthSize > bufferSize)
                    continue;

                var health = new byte[healthSize];
                Marshal.Copy(buffer + dataOffset, health, 0, healthSize);
                return health;
            }

            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ReadNvmeModel(SafeFileHandle handle)
    {
        const int identifySize = 4096;
        var bufferSize = 8 + 40 + identifySize;
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            Native.Zero(buffer, bufferSize);
            Marshal.WriteInt32(buffer, 0, Native.StorageDeviceProtocolSpecificProperty);
            Marshal.WriteInt32(buffer, 8, Native.ProtocolTypeNvme);
            Marshal.WriteInt32(buffer, 12, Native.NvmeDataTypeIdentify);
            Marshal.WriteInt32(buffer, 16, Native.NvmeIdentifyController);
            Marshal.WriteInt32(buffer, 24, 40);
            Marshal.WriteInt32(buffer, 28, identifySize);

            if (!Native.DeviceIoControl(
                    handle,
                    Native.IoctlStorageQueryProperty,
                    buffer,
                    (uint)bufferSize,
                    buffer,
                    (uint)bufferSize,
                    out _,
                    0))
                return string.Empty;

            var protocolOffset = Marshal.ReadInt32(buffer, 8 + 16);
            var dataOffset = 8 + protocolOffset;
            if (dataOffset < 0 || dataOffset + 64 > bufferSize)
                return string.Empty;

            var identify = new byte[64];
            Marshal.Copy(buffer + dataOffset, identify, 0, 64);
            return Encoding.ASCII.GetString(identify, 24, 40).Trim();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool QueryNvmeHealth(
        SafeFileHandle handle,
        nint buffer,
        int bufferSize,
        int propertyId,
        uint subValue)
    {
        const int protocolSize = 40;
        const int healthSize = 512;
        Native.Zero(buffer, bufferSize);
        Marshal.WriteInt32(buffer, 0, propertyId);
        Marshal.WriteInt32(buffer, 8, Native.ProtocolTypeNvme);
        Marshal.WriteInt32(buffer, 12, Native.NvmeDataTypeLogPage);
        Marshal.WriteInt32(buffer, 16, Native.NvmeHealthLogPage);
        Marshal.WriteInt32(buffer, 20, unchecked((int)subValue));
        Marshal.WriteInt32(buffer, 24, protocolSize);
        Marshal.WriteInt32(buffer, 28, healthSize);

        return Native.DeviceIoControl(
            handle,
            Native.IoctlStorageQueryProperty,
            buffer,
            (uint)bufferSize,
            buffer,
            (uint)bufferSize,
            out _,
            0);
    }

    private static byte[]? ReadAtaSmart(SafeFileHandle handle)
    {
        return ReadAtaSmartIoctl(handle) ?? ReadAtaPassThrough(handle);
    }

    private static byte[]? ReadAtaSmartIoctl(SafeFileHandle handle)
    {
        const int inSize = 32;
        const int outHeader = 16;
        const int smartSize = 512;
        var inPtr = Marshal.AllocHGlobal(inSize);
        var outPtr = Marshal.AllocHGlobal(outHeader + smartSize);
        try
        {
            Native.Zero(inPtr, inSize);
            Native.Zero(outPtr, outHeader + smartSize);
            Marshal.WriteInt32(inPtr, 0, smartSize);
            Marshal.WriteByte(inPtr, 4, 0xD0);
            Marshal.WriteByte(inPtr, 5, 1);
            Marshal.WriteByte(inPtr, 6, 1);
            Marshal.WriteByte(inPtr, 7, 0x4F);
            Marshal.WriteByte(inPtr, 8, 0xC2);
            Marshal.WriteByte(inPtr, 9, 0xA0);
            Marshal.WriteByte(inPtr, 10, 0xB0);

            if (!Native.DeviceIoControl(
                    handle,
                    Native.SmartRcvDriveData,
                    inPtr,
                    inSize,
                    outPtr,
                    (uint)(outHeader + smartSize),
                    out _,
                    0))
                return null;

            if (Marshal.ReadByte(outPtr, 4) != 0)
                return null;

            var smart = new byte[smartSize];
            Marshal.Copy(outPtr + outHeader, smart, 0, smartSize);
            return smart;
        }
        finally
        {
            Marshal.FreeHGlobal(inPtr);
            Marshal.FreeHGlobal(outPtr);
        }
    }

    private static byte[]? ReadAtaPassThrough(SafeFileHandle handle)
    {
        var headerSize = Marshal.SizeOf<Native.AtaPassThroughEx>();
        var bufferSize = headerSize + 512;
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            Native.Zero(buffer, bufferSize);
            var pass = new Native.AtaPassThroughEx
            {
                Length = (ushort)headerSize,
                AtaFlags = Native.AtaFlagsDataIn,
                DataTransferLength = 512,
                TimeOutValue = 10,
                DataBufferOffset = (nuint)headerSize,
                PreviousTaskFile = new byte[8],
                CurrentTaskFile = [0xD0, 1, 1, 0x4F, 0xC2, 0xA0, 0xB0, 0]
            };
            Marshal.StructureToPtr(pass, buffer, false);

            if (!Native.DeviceIoControl(
                    handle,
                    Native.IoctlAtaPassThrough,
                    buffer,
                    (uint)bufferSize,
                    buffer,
                    (uint)bufferSize,
                    out _,
                    0))
                return null;

            var smart = new byte[512];
            Marshal.Copy(buffer + headerSize, smart, 0, 512);
            return smart;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static class Native
    {
        public const uint GenericRead = 0x80000000;
        public const uint GenericWrite = 0x40000000;
        public const uint FileShareRead = 1;
        public const uint FileShareWrite = 2;
        public const uint OpenExisting = 3;
        public const uint IoctlStorageGetDeviceNumber = 0x2D1080;
        public const uint IoctlStorageQueryProperty = 0x2D1400;
        public const uint SmartRcvDriveData = 0x0007C088;
        public const uint IoctlAtaPassThrough = 0x0004D02C;
        public const int StorageDeviceProperty = 0;
        public const int StorageDeviceSeekPenaltyProperty = 7;
        public const int StorageDeviceTrimProperty = 8;
        public const int StorageAdapterProtocolSpecificProperty = 49;
        public const int StorageDeviceProtocolSpecificProperty = 50;
        public const int BusTypeNvme = 17;
        public const int ProtocolTypeNvme = 3;
        public const int NvmeDataTypeIdentify = 1;
        public const int NvmeDataTypeLogPage = 2;
        public const int NvmeIdentifyController = 1;
        public const int NvmeHealthLogPage = 2;
        public const ushort AtaFlagsDataIn = 0x02;

        public const uint FileFlagBackupSemantics = 0x02000000;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            nint lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            nint hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            nint lpInBuffer,
            uint nInBufferSize,
            nint lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            nint lpOverlapped);

        public static SafeFileHandle OpenVolume(string letter) =>
            OpenFirst($@"\\.\{letter}:", [0, GenericRead, GenericRead | GenericWrite], FileFlagBackupSemantics);

        public static SafeFileHandle OpenPhysicalDrive(int deviceNumber) =>
            OpenFirst(
                $@"\\.\PhysicalDrive{deviceNumber}",
                [GenericRead | GenericWrite, GenericRead, 0],
                0);

        private static SafeFileHandle OpenFirst(string path, uint[] accessRights, uint flags)
        {
            SafeFileHandle? last = null;
            foreach (var access in accessRights)
            {
                last?.Dispose();
                last = CreateFile(
                    path,
                    access,
                    FileShareRead | FileShareWrite,
                    0,
                    OpenExisting,
                    flags,
                    0);
                if (!last.IsInvalid)
                    return last;
            }

            return last!;
        }

        public static void Zero(nint ptr, int size)
        {
            var zeros = new byte[size];
            Marshal.Copy(zeros, 0, ptr, size);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct StorageDeviceNumber
        {
            public int DeviceType;
            public int DeviceNumber;
            public int PartitionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct StoragePropertyQuery
        {
            public int PropertyId;
            public int QueryType;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AtaPassThroughEx
        {
            public ushort Length;
            public ushort AtaFlags;
            public byte PathId;
            public byte TargetId;
            public byte Lun;
            public byte ReservedAsUchar;
            public uint DataTransferLength;
            public uint TimeOutValue;
            public uint ReservedAsUlong;
            public nuint DataBufferOffset;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] PreviousTaskFile;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] CurrentTaskFile;
        }
    }
}
