﻿// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3 with
// the following clarification and special exception.

// Linking this library statically or dynamically with other modules is
// making a combined work based on this library. Thus, the terms and
// conditions of the GNU Affero General Public License cover the whole
// combination.

// As a special exception, the copyright holders of this library give you
// permission to link this library with independent modules to produce an
// executable, regardless of the license terms of these independent
// modules, and to copy and distribute the resulting executable under
// terms of your choice, provided that you also meet, for each linked
// independent module, the terms and conditions of the license of that
// module. An independent module is a module which is not derived from
// or based on this library. If you modify this library, you may extend
// this exception to your version of the library, but you are not
// obligated to do so. If you do not wish to do so, delete this
// exception statement from your version.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;

namespace SilverSim.Updater
{
    [Serializable]
    public class InvalidPackageHashException : Exception
    {
        public InvalidPackageHashException()
        {
        }

        public InvalidPackageHashException(string message) : base(message)
        {
        }

        protected InvalidPackageHashException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public InvalidPackageHashException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    [Serializable]
    public class PackageDependencyFoundException : Exception
    {
        public PackageDependencyFoundException()
        {
        }

        public PackageDependencyFoundException(string message) : base(message)
        {
        }

        protected PackageDependencyFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public PackageDependencyFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class CoreUpdater
    {
        public string FeedUrl { get; private set; }
        public string PackageCachePath { get; }
        public string InstalledPackagesPath { get; }
        public string BinariesPath { get; }
        public string PluginsPath { get; }
        public string InterfaceVersion { get; }
        public string InstallRootPath { get; }
        public bool IsRestartRequired { get; private set; }
        private readonly Dictionary<string, PackageDescription> m_InstalledPackages = new Dictionary<string, PackageDescription>();
        private readonly Dictionary<string, PackageDescription> m_AvailablePackages = new Dictionary<string, PackageDescription>();
        private readonly Dictionary<string, bool> m_HiddenPackages = new Dictionary<string, bool>();
        private readonly ReaderWriterLock m_RwLock = new ReaderWriterLock();

        public enum LogType
        {
            Info,
            Warn,
            Error
        }

        public event Action<LogType, string> OnUpdateLog;

        private void PrintLog(LogType evtype, string message) => OnUpdateLog?.Invoke(evtype, message);

        public bool TryGetInstalledPackageDetails(string pkgname, out PackageDescription desc)
        {
            desc = null;
            m_RwLock.AcquireReaderLock(-1);
            try
            {
                PackageDescription found;
                if (m_InstalledPackages.TryGetValue(pkgname, out found))
                {
                    desc = new PackageDescription(found);
                    return true;
                }
            }
            finally
            {
                m_RwLock.ReleaseReaderLock();
            }
            return false;
        }

        public bool TryGetAvailablePackageDetails(string pkgname, out PackageDescription desc)
        {
            desc = null;
            m_RwLock.AcquireReaderLock(-1);
            try
            {
                PackageDescription found;
                if (m_AvailablePackages.TryGetValue(pkgname, out found))
                {
                    desc = new PackageDescription(found);
                    return true;
                }
            }
            finally
            {
                m_RwLock.ReleaseReaderLock();
            }
            return false;
        }

        public bool TryGetPackageDetails(string pkgname, out PackageDescription desc)
        {
            return TryGetInstalledPackageDetails(pkgname, out desc) || TryGetAvailablePackageDetails(pkgname, out desc);
        }

        public IReadOnlyDictionary<string, string> InstalledPackages
        {
            get
            {
                var pkgs = new Dictionary<string, string>();
                m_RwLock.AcquireReaderLock(-1);
                try
                {
                    foreach (PackageDescription pack in m_InstalledPackages.Values)
                    {
                        pkgs.Add(pack.Name, pack.Version);
                    }
                }
                finally
                {
                    m_RwLock.ReleaseReaderLock();
                }
                return pkgs;
            }
        }

        public IReadOnlyDictionary<string, string> AvailablePackages
        {
            get
            {
                var pkgs = new Dictionary<string, string>();
                m_RwLock.AcquireReaderLock(-1);
                try
                {
                    foreach (PackageDescription pack in m_AvailablePackages.Values)
                    {
                        bool ishidden;
                        if (!m_HiddenPackages.TryGetValue(pack.Name, out ishidden) || !ishidden)
                        {
                            pkgs.Add(pack.Name, pack.Version);
                        }
                    }
                }
                finally
                {
                    m_RwLock.ReleaseReaderLock();
                }
                return pkgs;
            }
        }

        public IReadOnlyList<string> GetPreloadAssemblies(string mode)
        {
            var preloadAssemblies = new List<string>();
            m_RwLock.AcquireReaderLock(-1);
            try
            {
                foreach (PackageDescription pack in m_InstalledPackages.Values)
                {
                    foreach (PackageDescription.PreloadAssembly preloadAssembly in pack.PreloadAssemblies)
                    {
                        string defConfig = preloadAssembly.Filename;
                        if (defConfig?.Length != 0 && (preloadAssembly.StartTypes.Count == 0 || preloadAssembly.StartTypes.Contains(mode)) &&
                            !preloadAssemblies.Contains(preloadAssembly.Filename))
                        {
                            preloadAssemblies.Add(preloadAssembly.Filename);
                        }
                    }
                }
            }
            finally
            {
                m_RwLock.ReleaseReaderLock();
            }
            return preloadAssemblies;
        }

        public IReadOnlyList<string> GetDefaultConfigurationFiles(string mode)
        {
            var configs = new List<string>();
            m_RwLock.AcquireReaderLock(-1);
            try
            {
                foreach (PackageDescription pack in m_InstalledPackages.Values)
                {
                    foreach (PackageDescription.Configuration cfg in pack.DefaultConfigurations)
                    {
                        string defConfig = cfg.Source;
                        if (defConfig?.Length != 0 && (cfg.StartTypes.Count == 0 || cfg.StartTypes.Contains(mode)))
                        {
                            configs.Add(cfg.Source);
                        }
                    }
                }
            }
            finally
            {
                m_RwLock.ReleaseReaderLock();
            }
            return configs;
        }

        static public readonly CoreUpdater Instance = new CoreUpdater();

        public bool LoadUpdaterConfig()
        {
            string cfgfile = Assembly.GetExecutingAssembly().Location + ".config";
            if (File.Exists(cfgfile))
            {
                var doc = new XmlDocument
                {
                    XmlResolver = null
                };
                doc.Load(cfgfile);
                XmlNodeList elemList = doc.GetElementsByTagName("configuration");
                if (elemList?.Count > 0)
                {
                    var elem = elemList[0] as XmlElement;
                    if (elem != null)
                    {
                        elemList = elem.GetElementsByTagName("feed-url");
                        if (elemList?.Count > 0)
                        {
                            elem = elemList[0] as XmlElement;
                            if (elem != null)
                            {
                                FeedUrl = elem.InnerText;
                                if (!FeedUrl.EndsWith("/"))
                                {
                                    FeedUrl += "/";
                                }
                            }
                        }
                    }
                }
                return true;
            }
            return false;
        }

        private CoreUpdater()
        {
            InstallRootPath = Path.GetFullPath(Path.Combine(Assembly.GetExecutingAssembly().Location, "../.."));
            PackageCachePath = Path.Combine(InstallRootPath, "data/dl-cache");
            Directory.CreateDirectory(PackageCachePath);
            InstalledPackagesPath = Path.Combine(InstallRootPath, "bin/installed-packages");
            BinariesPath = Path.Combine(InstallRootPath, "bin");
            PluginsPath = Path.Combine(InstallRootPath, "bin/plugins");
            Directory.CreateDirectory(InstalledPackagesPath);
            InterfaceVersion = string.Empty;
            if (LoadUpdaterConfig() && FeedUrl?.Length != 0)
            {
                try
                {
                    using (Stream i = new FileStream(Path.Combine(InstalledPackagesPath, "SilverSim.Updater.Cfg.spkg"), FileMode.Open))
                    {
                        var desc = new PackageDescription(i);
                        InterfaceVersion = desc.InterfaceVersion;
                    }
                }
                catch
                {
                    /* if interface version is not set, we simply switch to disabled */
                    InterfaceVersion = string.Empty;
                }
            }

            PrintLog(LogType.Info, "Cleanup up old installation files");
            foreach (string deletefile in Directory.GetFiles(Path.Combine(InstallRootPath, "bin"), "*.delete", SearchOption.AllDirectories))
            {
                File.Delete(deletefile);
            }
        }

        public void CheckForUpdates()
        {
            if(InterfaceVersion?.Length == 0)
            {
                PrintLog(LogType.Error, "Update system is disabled");
                return;
            }
            LoadInstalledPackageDescriptions();
            UpdatePackageFeed();
            var updatable = new List<string>();
            m_RwLock.AcquireReaderLock(-1);
            try
            {
                foreach (KeyValuePair<string, PackageDescription> kvp in m_InstalledPackages)
                {
                    PackageDescription current;
                    if (m_AvailablePackages.TryGetValue(kvp.Key, out current) && current.Version != kvp.Value.Version)
                    {
                        updatable.Add(kvp.Key);
                    }
                }
            }
            finally
            {
                m_RwLock.ReleaseReaderLock();
            }

            foreach(string pkg in updatable)
            {
                InstallPackage(pkg);
            }
        }

        public void LoadInstalledPackageDescriptions()
        {
            string[] pkgfiles = Directory.GetFiles(InstalledPackagesPath, "*.spkg");
            m_RwLock.AcquireWriterLock(-1);
            try
            {
                m_InstalledPackages.Clear();
                foreach (string pkgfile in pkgfiles)
                {
                    using (var i = new FileStream(pkgfile, FileMode.Open))
                    {
                        PackageDescription desc;
                        try
                        {
                            desc = new PackageDescription(i);
                        }
                        catch (Exception e)
                        {
                            throw new InvalidDataException("Failed to load package description " + pkgfile, e);
                        }
                        try
                        {
                            m_InstalledPackages.Add(desc.Name, desc);
                        }
                        catch
                        {
                            throw new ArgumentException(string.Format("Installed package {0} is duplicate in {1}.", desc.Name, pkgfile));
                        }
                    }
                }
            }
            finally
            {
                m_RwLock.ReleaseWriterLock();
            }
        }

        #region Package feed handling
        public bool UpdatePackageFeed()
        {
            if(InterfaceVersion?.Length == 0)
            {
                /* debugging does not have any package data normally, so we skip loading the feed */
                PrintLog(LogType.Error, "Update system is disabled");
                return false;
            }

            PrintLog(LogType.Info, "Updating package feed");
            var additionalpackagestofetch = new List<string>();
            using (var reader = new XmlTextReader(FeedUrl + InterfaceVersion + "/packages.list")
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null
            })
            {
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            string packagename = string.Empty;
                            bool isHidden = false;
                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    switch (reader.Name)
                                    {
                                        case "name":
                                            packagename = reader.Value;
                                            break;

                                        case "hidden":
                                            isHidden = bool.Parse(reader.Value);
                                            break;

                                        default:
                                            break;
                                    }
                                }
                                while (reader.MoveToNextAttribute());

                                if (packagename?.Length != 0)
                                {
                                    m_HiddenPackages[packagename] = isHidden;
                                    if (!m_AvailablePackages.ContainsKey(packagename))
                                    {
                                        additionalpackagestofetch.Add(packagename);
                                    }
                                }
                            }
                            break;

                        default:
                            break;
                    }
                }
            }

            foreach(KeyValuePair<string, PackageDescription> kvp in m_InstalledPackages)
            {
                var current = new PackageDescription(FeedUrl + InterfaceVersion + "/" + kvp.Key + ".spkg");
                m_RwLock.AcquireWriterLock(-1);
                try
                {
                    m_AvailablePackages[current.Name] = current;
                }
                finally
                {
                    m_RwLock.ReleaseWriterLock();
                }
            }

            foreach(string package in additionalpackagestofetch)
            {
                var current = new PackageDescription(FeedUrl + InterfaceVersion + "/" + package + ".spkg");
                m_RwLock.AcquireWriterLock(-1);
                try
                {
                    m_AvailablePackages[current.Name] = current;
                }
                finally
                {
                    m_RwLock.ReleaseWriterLock();
                }
            }
            PrintLog(LogType.Info, "Updated package feed");
            return true;
        }

        public bool AreUpdatesAvailable
        {
            get
            {
                if(m_AvailablePackages.Count == 0)
                {
                    UpdatePackageFeed();
                }
                m_RwLock.AcquireReaderLock(-1);
                try
                {
                    foreach (KeyValuePair<string, PackageDescription> kvp in m_InstalledPackages)
                    {
                        PackageDescription current;
                        if (m_AvailablePackages.TryGetValue(kvp.Key, out current) && current.Version != kvp.Value.Version)
                        {
                            return true;
                        }
                    }
                }
                finally
                {
                    m_RwLock.ReleaseReaderLock();
                }
                return false;
            }
        }
        #endregion

        private bool DoesFileRequireReplacement(string fname) =>
            fname.EndsWith(".dll") || fname.EndsWith(".exe") || fname.EndsWith(".so") || fname.EndsWith(".dylib");

        public void UninstallPackage(string packagename)
        {
            m_RwLock.AcquireReaderLock(-1);
            try
            {
                foreach (PackageDescription packsearch in m_InstalledPackages.Values)
                {
                    if (packsearch.Dependencies.Keys.Contains(packagename))
                    {
                        throw new PackageDependencyFoundException(packsearch.Name);
                    }
                }
            }
            finally
            {
                m_RwLock.ReleaseReaderLock();
            }

            PrintLog(LogType.Info, "Uninstalling package " + packagename);
            PackageDescription pack = m_InstalledPackages[packagename];
            File.Delete(Path.Combine(InstalledPackagesPath, pack.Name + ".spkg"));
            foreach(KeyValuePair<string, PackageDescription.FileInfo> kvp in pack.Files)
            {
                string fPath = Path.Combine(InstallRootPath, kvp.Key);
                if(DoesFileRequireReplacement(kvp.Key) && !File.Exists(fPath + ".delete"))
                {
                    File.Move(fPath, fPath + ".delete");
                }
                if (File.Exists(fPath))
                {
                    File.Delete(fPath);
                }
            }
            m_RwLock.AcquireWriterLock(-1);
            try
            {
                m_InstalledPackages.Remove(pack.Name);
            }
            finally
            {
                m_RwLock.ReleaseWriterLock();
            }
            PrintLog(LogType.Info, "Uninstalled package " + packagename);
        }

        private PackageDescription InstallPackageNoDependencies(string packagename, string version = "")
        {
            PackageDescription current = version?.Length == 0 ?
                new PackageDescription(FeedUrl + InterfaceVersion + "/" + packagename + ".spkg") :
                new PackageDescription(FeedUrl + InterfaceVersion + "/" + version + "/" + packagename + ".spkg");
            PrintLog(LogType.Info, "Installing package " + packagename + " (" + current.Version + ") without dependency check");
            DownloadPackage(current);
            UnpackPackage(current);
            PrintLog(LogType.Info, "Installed package " + packagename + " (" + current.Version + ") without dependency check");
            return current;
        }

        public void InstallPackage(string packagename)
        {
            var requiredPackages = new Dictionary<string, PackageDescription>();
            var newDependencies = new Dictionary<string, string>
            {
                [packagename] = string.Empty
            };
            while (newDependencies.Count != 0)
            {
                string pkg = newDependencies.Keys.First();
                string version = newDependencies[pkg];
                newDependencies.Remove(pkg);
                PackageDescription current = version?.Length == 0 ?
                    new PackageDescription(FeedUrl + InterfaceVersion + "/" + pkg + ".spkg") :
                    new PackageDescription(FeedUrl + InterfaceVersion + "/" + version +  "/" + pkg + ".spkg");
                requiredPackages.Add(current.Name, current);
                foreach (KeyValuePair<string, string> dep in current.Dependencies)
                {
                    if (requiredPackages.ContainsKey(dep.Key) ||
                        (m_InstalledPackages.ContainsKey(dep.Key) &&
                        (dep.Value?.Length == 0 || dep.Value == m_InstalledPackages[dep.Key].Version)))
                    {
                        continue;
                    }
                    if (!newDependencies.ContainsKey(dep.Key))
                    {
                        newDependencies.Add(dep.Key, dep.Value);
                    }
                }
            }

            foreach(PackageDescription package in requiredPackages.Values)
            {
                PrintLog(LogType.Info, "Installing package " + package.Name + " (" + package.Version + ")");
                DownloadPackage(package);
                UnpackPackage(package);
                PrintLog(LogType.Info, "Installed package " + package.Name + " (" + package.Version + ")");
            }
        }

        private string GetCacheFileName(PackageDescription package) =>
            Path.Combine(PackageCachePath, package.InterfaceVersion + "-" + package.Version + "-" + package.Name + ".zip");

        #region Installation verification
        public bool IsInstallationValid
        {
            get
            {
                foreach(PackageDescription pack in m_InstalledPackages.Values)
                {
                    if(!VerifyInstalledPackage(pack))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public void VerifyInstallation()
        {
            List<PackageDescription> installedpackages;

            m_RwLock.AcquireReaderLock(-1);
            try
            {
                installedpackages = new List<PackageDescription>(m_InstalledPackages.Values);
            }
            finally
            {
                m_RwLock.ReleaseReaderLock();
            }
            foreach (PackageDescription pack in installedpackages)
            {
                PrintLog(LogType.Info, "Verifying package " + pack.Name + " (" + pack.Version + ")");
                if (!VerifyInstalledPackage(pack))
                {
                    PrintLog(LogType.Info, "Re-Installing package " + pack.Name + " (" + pack.Version + ")");
                    DownloadPackage(pack);
                    UnpackPackage(pack);
                    PrintLog(LogType.Info, "Re-Installed package " + pack.Name + " (" + pack.Version + ")");
                }
            }

            var unresolvedDependencies = new Dictionary<string, string>();
            m_RwLock.AcquireReaderLock(-1);
            try
            {
                foreach (PackageDescription pack in m_InstalledPackages.Values)
                {
                    foreach (KeyValuePair<string, string> kvp in pack.Dependencies)
                    {
                        if (!m_InstalledPackages.ContainsKey(kvp.Key) && !unresolvedDependencies.ContainsKey(kvp.Key))
                        {
                            unresolvedDependencies.Add(kvp.Key, kvp.Value);
                        }
                    }
                }
            }
            finally
            {
                m_RwLock.ReleaseReaderLock();
            }

            while(unresolvedDependencies.Count != 0)
            {
                KeyValuePair<string, string> unresolvedPackage = unresolvedDependencies.First<KeyValuePair<string, string>>();
                unresolvedDependencies.Remove(unresolvedPackage.Key);

                m_RwLock.AcquireReaderLock(-1);
                try
                {
                    if (m_InstalledPackages.ContainsKey(unresolvedPackage.Key))
                    {
                        /* do not re-install if already installed */
                        continue;
                    }
                }
                finally
                {
                    m_RwLock.ReleaseReaderLock();
                }

                PackageDescription pack = InstallPackageNoDependencies(unresolvedPackage.Key, unresolvedPackage.Value);

                m_RwLock.AcquireReaderLock(-1);
                try
                {
                    foreach (KeyValuePair<string, string> kvp in pack.Dependencies)
                    {
                        if (!m_InstalledPackages.ContainsKey(kvp.Key) && !unresolvedDependencies.ContainsKey(kvp.Key))
                        {
                            unresolvedDependencies.Add(kvp.Key, kvp.Value);
                        }
                    }
                }
                finally
                {
                    m_RwLock.ReleaseReaderLock();
                }
            }
        }

        private bool IsHashEqual(byte[] a, byte[] b)
        {
            if(a.Length == b.Length)
            {
                for(int i = 0; i <a.Length; ++i)
                {
                    if(a[i] != b[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        private bool VerifyInstalledPackage(PackageDescription pack)
        {
            foreach (KeyValuePair<string, PackageDescription.FileInfo> kvp in pack.Files)
            {
                if (kvp.Value.Hash != null)
                {
                    using (SHA256 hash = SHA256.Create())
                    {
                        try
                        {
                            using (FileStream fs = new FileStream(Path.Combine(InstallRootPath, kvp.Key), FileMode.Open, FileAccess.Read))
                            {
                                hash.ComputeHash(fs);
                            }
                        }
                        catch(FileNotFoundException)
                        {
                            return false;
                        }
                        catch(DirectoryNotFoundException)
                        {
                            return false;
                        }

                        if (!IsHashEqual(hash.Hash, kvp.Value.Hash))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        #endregion

        private void UnpackPackage(PackageDescription package)
        {
            string cachefile = GetCacheFileName(package);

            try
            {
                using (SHA256 hash = SHA256.Create())
                {
                    using (var fs = new FileStream(cachefile, FileMode.Open, FileAccess.Read))
                    {
                        hash.ComputeHash(fs);
                    }

                    if (!IsHashEqual(hash.Hash, package.Hash))
                    {
                        throw new InvalidPackageHashException("Package " + package.Name + " is invalid");
                    }
                }
            }
            catch
            {
                File.Delete(cachefile);
                throw;
            }

            m_RwLock.AcquireWriterLock(-1);
            try
            {
                package.WriteFile(Path.Combine(InstalledPackagesPath, package.Name + ".spkg"));

                using (var fs = new FileStream(cachefile, FileMode.Open))
                {
                    using (var zip = new ZipArchive(fs))
                    {
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            using (Stream i = entry.Open())
                            {
                                string targetFile = Path.Combine(InstallRootPath, entry.FullName);
                                if (DoesFileRequireReplacement(entry.FullName))
                                {
                                    if (!File.Exists(targetFile + ".delete") && File.Exists(targetFile))
                                    {
                                        try
                                        {
                                            File.Delete(targetFile);
                                        }
                                        catch
                                        {
                                            File.Move(targetFile, targetFile + ".delete");
                                        }
                                    }
                                    if (File.Exists(targetFile + ".delete"))
                                    {
                                        IsRestartRequired = true;
                                    }
                                }
                                if (File.Exists(targetFile))
                                {
                                    File.Delete(targetFile);
                                }
                                string targetDir = Path.GetFullPath(Path.Combine(targetFile, ".."));
                                if (!Directory.Exists(targetDir))
                                {
                                    Directory.CreateDirectory(targetDir);
                                }
                                using (var o = new FileStream(targetFile, FileMode.Create))
                                {
                                    i.CopyTo(o);
                                }
                            }
                        }
                    }
                }
                m_InstalledPackages[package.Name] = new PackageDescription(package);
            }
            finally
            {
                m_RwLock.ReleaseWriterLock();
            }
        }

        private void DownloadPackage(PackageDescription package)
        {
            string cachefile = GetCacheFileName(package);
            if (!File.Exists(cachefile))
            {
                WebRequest webreq = WebRequest.Create(FeedUrl + package.InterfaceVersion + "/" + package.Version + "/" + package.Name + ".zip");
                try
                {
                    using (Stream s = webreq.GetResponse().GetResponseStream())
                    {
                        if(File.Exists(cachefile))
                        {
                            File.Delete(cachefile);
                        }
                        using (var fs = new FileStream(cachefile, FileMode.Create))
                        {
                            s.CopyTo(fs);
                        }
                    }
                }
                catch
                {
                    File.Delete(cachefile);
                    throw;
                }
            }
        }
    }
}
