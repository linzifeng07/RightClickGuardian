using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RightClickGuardian
{
    public static class CategoryNames
    {
        public const string File = "文件";
        public const string Folder = "文件夹";
        public const string Directory = "目录";
        public const string DirectoryBackground = "目录背景";
        public const string DesktopBackground = "桌面背景";
        public const string Drive = "磁盘分区";
        public const string AllObjects = "所有对象";
        public const string ThisPc = "此电脑";
        public const string RecycleBin = "回收站";
        public const string Library = "库";
        public const string NewMenu = "新建菜单";
        public const string SendTo = "发送到";
        public const string OpenWith = "打开方式";
        public const string WinX = "Win+X";
        public const string ImageMedia = "图片与媒体";
        public const string ModernApps = "现代应用";
        public const string CommandStore = "命令仓库";
        public const string Lab = "右键实验室";
        public const string Software = "软件专区";

        public static readonly string[] Ordered = new[]
        {
            File, Folder, Directory, DirectoryBackground, DesktopBackground,
            Drive, AllObjects, ThisPc, RecycleBin, Library,
            ImageMedia, ModernApps, NewMenu, SendTo, OpenWith, WinX, CommandStore
        };
    }

    public enum EntryKind
    {
        StaticVerb,
        ContextHandler,
        ModernVerb,
        ShellNew,
        SendToFile,
        OpenWithApplication,
        WinXFile
    }

    [DataContract]
    public sealed class MenuEntry
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Category { get; set; }
        [DataMember] public EntryKind Kind { get; set; }
        [DataMember] public string Scope { get; set; }
        [DataMember] public string RegistryPath { get; set; }
        [DataMember] public string RegistryValueName { get; set; }
        [DataMember] public string Clsid { get; set; }
        [DataMember] public string Command { get; set; }
        [DataMember] public string FilePath { get; set; }
        [DataMember] public string PackageName { get; set; }
        [DataMember] public string VerbId { get; set; }
        [DataMember] public string Source { get; set; }
        [DataMember] public string IconHint { get; set; }
        [DataMember] public bool Enabled { get; set; }
        [DataMember] public bool Protected { get; set; }
        [DataMember] public bool IsMicrosoft { get; set; }
        [DataMember] public bool IsCritical { get; set; }
        [DataMember] public string Details { get; set; }

        public MenuEntry()
        {
            Id = "";
            Name = "";
            Category = CategoryNames.File;
            Scope = "";
            RegistryPath = "";
            RegistryValueName = "";
            Clsid = "";
            Command = "";
            FilePath = "";
            PackageName = "";
            VerbId = "";
            Source = "";
            IconHint = "";
            Details = "";
            Enabled = true;
        }
    }

    [DataContract]
    public sealed class PolicyRule
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Category { get; set; }
        [DataMember] public EntryKind Kind { get; set; }
        [DataMember] public string Scope { get; set; }
        [DataMember] public string RegistryPath { get; set; }
        [DataMember] public string RegistryValueName { get; set; }
        [DataMember] public string Clsid { get; set; }
        [DataMember] public string FilePath { get; set; }
        [DataMember] public string PackageName { get; set; }
        [DataMember] public string VerbId { get; set; }
        [DataMember] public string IconHint { get; set; }
        [DataMember] public bool LegacyDisableOriginallyPresent { get; set; }
        [DataMember] public bool ProgrammaticAccessOnlyOriginallyPresent { get; set; }
        [DataMember] public bool NoOpenWithOriginallyPresent { get; set; }
        [DataMember] public bool UserBlockOriginallyPresent { get; set; }
        [DataMember] public bool MachineBlockOriginallyPresent { get; set; }
        [DataMember] public string BackupPath { get; set; }
        [DataMember] public DateTime DisabledAtUtc { get; set; }

        public PolicyRule()
        {
            Id = "";
            Name = "";
            Category = "";
            Scope = "";
            RegistryPath = "";
            RegistryValueName = "";
            Clsid = "";
            FilePath = "";
            PackageName = "";
            VerbId = "";
            IconHint = "";
            BackupPath = "";
        }
    }

    [DataContract]
    public sealed class PolicyDocument
    {
        [DataMember] public int Version { get; set; }
        [DataMember] public bool GuardEnabled { get; set; }
        [DataMember] public int GuardIntervalMilliseconds { get; set; }
        [DataMember] public DateTime UpdatedAtUtc { get; set; }
        [DataMember] public List<PolicyRule> Rules { get; set; }

        public PolicyDocument()
        {
            Version = 1;
            GuardEnabled = true;
            GuardIntervalMilliseconds = 1500;
            UpdatedAtUtc = DateTime.UtcNow;
            Rules = new List<PolicyRule>();
        }
    }

    public sealed class ScanResult
    {
        public List<MenuEntry> Entries { get; set; }
        public List<string> Warnings { get; private set; }
        public DateTime CompletedAt { get; set; }

        public ScanResult()
        {
            Entries = new List<MenuEntry>();
            Warnings = new List<string>();
        }
    }
}
