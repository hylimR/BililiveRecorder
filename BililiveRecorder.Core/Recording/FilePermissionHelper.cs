using System;
using System.IO;
using Serilog;

namespace BililiveRecorder.Core.Recording
{
    /// <summary>
    /// Helper class to set file permissions based on UMASK environment variable
    /// </summary>
    internal static class FilePermissionHelper
    {
        private static int? cachedUmask = null;

        /// <summary>
        /// Applies file permissions based on UMASK environment variable
        /// </summary>
        /// <param name="filePath">Full path to the file</param>
        /// <param name="logger">Logger instance for diagnostics</param>
        public static void ApplyUmaskPermissions(string filePath, ILogger? logger = null)
        {
#if NET6_0_OR_GREATER
            // Only apply on Unix-like systems
            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
                return;

            try
            {
                var umask = GetUmask();
                if (!umask.HasValue)
                    return;

                // Default file permissions before umask: 666 (rw-rw-rw-)
                // Umask 022 results in: 644 (rw-r--r--)
                // Umask 755 is unusual but user wants: 755 (rwxr-xr-x)

                // For UMASK=755, the user likely wants the file to have 755 permissions
                // This is non-standard umask usage but we'll support it
                var permissions = ParseUmaskAsPermissions(umask.Value);

                if (permissions.HasValue)
                {
                    File.SetUnixFileMode(filePath, permissions.Value);
                    logger?.Debug("应用文件权限 {Permissions} 到 {FilePath} (UMASK={Umask})",
                        Convert.ToString((int)permissions.Value, 8).PadLeft(3, '0'),
                        filePath,
                        Convert.ToString(umask.Value, 8).PadLeft(3, '0'));
                }
            }
            catch (Exception ex)
            {
                logger?.Warning(ex, "设置文件权限时出错");
            }
#endif
        }

        private static int? GetUmask()
        {
            if (cachedUmask.HasValue)
                return cachedUmask;

            var umaskStr = Environment.GetEnvironmentVariable("UMASK");
            if (string.IsNullOrWhiteSpace(umaskStr))
                return null;

            // Try to parse as octal
            try
            {
                cachedUmask = Convert.ToInt32(umaskStr, 8);
                return cachedUmask;
            }
            catch
            {
                return null;
            }
        }

#if NET6_0_OR_GREATER
        private static UnixFileMode? ParseUmaskAsPermissions(int umask)
        {
            // If umask looks like a permission value (> 64 decimal = 100 octal), treat it as direct permissions
            if (umask >= 64)
            {
                // User provided something like 755 - treat as direct permission
                return OctalToUnixFileMode(umask);
            }
            else
            {
                // Standard umask behavior: apply to default file permissions (666 octal = 438 decimal)
                var permissions = 438 & ~umask;
                return OctalToUnixFileMode(permissions);
            }
        }

        private static UnixFileMode OctalToUnixFileMode(int octal)
        {
            var mode = UnixFileMode.None;

            // Owner permissions
            if ((octal & 256) != 0) mode |= UnixFileMode.UserRead;    // 0400 octal = 256 decimal
            if ((octal & 128) != 0) mode |= UnixFileMode.UserWrite;   // 0200 octal = 128 decimal
            if ((octal & 64) != 0) mode |= UnixFileMode.UserExecute;  // 0100 octal = 64 decimal

            // Group permissions
            if ((octal & 32) != 0) mode |= UnixFileMode.GroupRead;    // 0040 octal = 32 decimal
            if ((octal & 16) != 0) mode |= UnixFileMode.GroupWrite;   // 0020 octal = 16 decimal
            if ((octal & 8) != 0) mode |= UnixFileMode.GroupExecute;  // 0010 octal = 8 decimal

            // Other permissions
            if ((octal & 4) != 0) mode |= UnixFileMode.OtherRead;     // 0004 octal = 4 decimal
            if ((octal & 2) != 0) mode |= UnixFileMode.OtherWrite;    // 0002 octal = 2 decimal
            if ((octal & 1) != 0) mode |= UnixFileMode.OtherExecute;  // 0001 octal = 1 decimal

            return mode;
        }
#endif
    }
}
