using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.AccessControl;
using System.Security.Principal;

namespace RightClickGuardian
{
    public sealed class PolicyStore
    {
        private static readonly string DataOverride =
            Environment.GetEnvironmentVariable("RIGHT_CLICK_GUARDIAN_DATA_DIR");
        public static readonly string RootDirectory =
            string.IsNullOrWhiteSpace(DataOverride)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "RightClickGuardian")
                : DataOverride;
        public static readonly string BackupDirectory = Path.Combine(RootDirectory, "backups");
        public static readonly string QuarantineDirectory = Path.Combine(RootDirectory, "quarantine");
        public static readonly string PolicyPath = Path.Combine(RootDirectory, "policy.json");
        public static readonly string PolicyBackupPath = Path.Combine(RootDirectory, "policy.backup.json");
        private readonly object sync = new object();

        public PolicyStore()
        {
            EnsureDirectories();
        }

        public PolicyDocument Load()
        {
            lock (sync)
            {
                PolicyDocument document = TryLoad(PolicyPath);
                if (document != null) return document;
                document = TryLoad(PolicyBackupPath);
                if (document != null)
                {
                    Save(document);
                    return document;
                }
                return new PolicyDocument();
            }
        }

        public void Save(PolicyDocument document)
        {
            lock (sync)
            {
                EnsureDirectories();
                document.UpdatedAtUtc = DateTime.UtcNow;
                string temporaryPath = PolicyPath + ".tmp";
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PolicyDocument));
                using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    serializer.WriteObject(stream, document);
                    stream.Flush(true);
                }
                if (File.Exists(PolicyPath))
                {
                    File.Copy(PolicyPath, PolicyBackupPath, true);
                    File.Delete(PolicyPath);
                }
                File.Move(temporaryPath, PolicyPath);
                if (!File.Exists(PolicyBackupPath)) File.Copy(PolicyPath, PolicyBackupPath, true);
            }
        }

        private static PolicyDocument TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PolicyDocument));
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return serializer.ReadObject(stream) as PolicyDocument;
                }
            }
            catch
            {
                return null;
            }
        }

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(RootDirectory);
            Directory.CreateDirectory(BackupDirectory);
            Directory.CreateDirectory(QuarantineDirectory);
            if (string.IsNullOrWhiteSpace(DataOverride)) TryProtectDirectory(RootDirectory);
        }

        private static void TryProtectDirectory(string path)
        {
            try
            {
                DirectoryInfo info = new DirectoryInfo(path);
                DirectorySecurity security = info.GetAccessControl();
                security.SetAccessRuleProtection(true, false);
                SecurityIdentifier administrators =
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                SecurityIdentifier system =
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                FileSystemAccessRule adminsRule = new FileSystemAccessRule(
                    administrators, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow);
                FileSystemAccessRule systemRule = new FileSystemAccessRule(
                    system, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow);
                security.AddAccessRule(adminsRule);
                security.AddAccessRule(systemRule);
                info.SetAccessControl(security);
            }
            catch
            {
                // Protection is best-effort. The guard still re-applies the policy.
            }
        }
    }
}
