using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gobchat.Core.Runtime
{
    public static class GobchatContext
    {
        private const string AppDataFolderName = "GobchatEx";
        private const string LegacyAppDataFolderName = "Gobchat";

        public static string ResourceLocation
        {
            get { return System.IO.Path.Combine(ApplicationLocation, @"resources"); }
        }

        public static string AppDataLocation
        {
#if DEBUG
            get { return System.IO.Path.Combine(ApplicationLocation, "DebugConfig"); }
#else
            get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName); }
#endif
        }

        public static string AppConfigLocation
        {
            get { return System.IO.Path.Combine(AppDataLocation, "config"); }
        }

        /// <summary>
        /// True when a legacy <c>%AppData%\Gobchat</c> folder is present and has not yet been migrated
        /// (i.e. <c>%AppData%\GobchatEx</c> does not exist) - the only situation in which
        /// <see cref="MigrateLegacyAppData"/> would actually copy anything. The first-run screen uses this
        /// to decide whether to offer the "migrate old profiles" choice. Always false in DEBUG, where the
        /// local DebugConfig folder is used and migration is a no-op.
        /// </summary>
        public static bool HasLegacyAppData
        {
            get
            {
#if DEBUG
                return false;
#else
                var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var legacyPath = System.IO.Path.Combine(roaming, LegacyAppDataFolderName);
                var newPath = System.IO.Path.Combine(roaming, AppDataFolderName);
                return System.IO.Directory.Exists(legacyPath) && !System.IO.Directory.Exists(newPath);
#endif
            }
        }

        public static string ApplicationLocation
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string ApplicationName
        {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Name; }
        }

        public static GobVersion ApplicationVersion
        {
            get { return new GobVersion(InnerApplicationVersion); }
        }

        private static Version InnerApplicationVersion
        {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version; }
        }

        /// <summary>
        /// One-time, non-destructive migration of the user data folder from the pre-rebrand
        /// location (%AppData%\Gobchat) to %AppData%\GobchatEx. Only runs if the new folder
        /// does not yet exist and the legacy folder does; the legacy folder is left untouched.
        /// To avoid a half-migrated state, the data is copied into a temporary folder first and
        /// only moved into place once the copy succeeds. No-op in DEBUG (the local DebugConfig
        /// folder is used there instead). May throw on I/O errors - callers should guard.
        /// </summary>
        public static void MigrateLegacyAppData()
        {
#if !DEBUG
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var legacyPath = System.IO.Path.Combine(roaming, LegacyAppDataFolderName);
            var newPath = System.IO.Path.Combine(roaming, AppDataFolderName);

            if (System.IO.Directory.Exists(newPath) || !System.IO.Directory.Exists(legacyPath))
                return;

            var tempPath = newPath + ".migrating";
            if (System.IO.Directory.Exists(tempPath))
                System.IO.Directory.Delete(tempPath, true);

            try
            {
                CopyDirectory(legacyPath, tempPath);
                System.IO.Directory.Move(tempPath, newPath);
            }
            catch
            {
                if (System.IO.Directory.Exists(tempPath))
                    System.IO.Directory.Delete(tempPath, true);
                throw;
            }
#endif
        }

#if !DEBUG
        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            System.IO.Directory.CreateDirectory(targetDir);

            foreach (var file in System.IO.Directory.GetFiles(sourceDir))
                System.IO.File.Copy(file, System.IO.Path.Combine(targetDir, System.IO.Path.GetFileName(file)), overwrite: false);

            foreach (var dir in System.IO.Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, System.IO.Path.Combine(targetDir, System.IO.Path.GetFileName(dir)));
        }
#endif

    }
}