// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Xml;

namespace SilverSim.Updater
{
    [Serializable]
    public class InvalidPackageHashException : Exception
    {
        public InvalidPackageHashException() { }
        public InvalidPackageHashException(string message) : base(message) { }
        protected InvalidPackageHashException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public InvalidPackageHashException(string message, Exception innerException) : base(message, innerException) { }
    }

    [Serializable]
    public class PackageDependencyFoundException : Exception
    {
        public PackageDependencyFoundException() { }
        public PackageDependencyFoundException(string message) : base(message) { }
        protected PackageDependencyFoundException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public PackageDependencyFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class CoreUpdater
    {
        public string FeedUrl { get; private set; }
        public string PackageCachePath { get; private set; }
        public string InstalledPackagesPath { get; private set; }
        public string InterfaceVersion { get; private set; }
        public string InstallRootPath { get; private set; }
        public bool IsRestartRequired { get; private set; }
        Dictionary<string, PackageDescription> m_InstalledPackages = new Dictionary<string, PackageDescription>();
        Dictionary<string, PackageDescription> m_AvailablePackages = new Dictionary<string, PackageDescription>();
        public IReadOnlyDictionary<string, string> InstalledPackages
        {
            get
            {
                Dictionary<string, string> pkgs = new Dictionary<string, string>();
                foreach(PackageDescription pack in m_InstalledPackages.Values)
                {
                    pkgs.Add(pack.Name, pack.Version);
                }
                return pkgs;
            }
        }

        public IReadOnlyList<string> GetDefaultConfigurationFiles(string mode)
        {
            List<string> configs = new List<string>();
            foreach (PackageDescription pack in m_InstalledPackages.Values)
            {
                foreach (PackageDescription.Configuration cfg in pack.DefaultConfigurations)
                {
                    string defConfig = cfg.Source;
                    if (!string.IsNullOrEmpty(defConfig) && (cfg.StartTypes.Count == 0 || cfg.StartTypes.Contains(mode)))
                    {
                        configs.Add(cfg.Source);
                    }
                }
            }
            return configs;
        }

        static public CoreUpdater Instance = new CoreUpdater();

        public bool LoadUpdaterConfig()
        {
            if (File.Exists("SilverSim.Updater.dll.config"))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load("SilverSim.Updater.dll.config");
                XmlNodeList elemList = doc.GetElementsByTagName("configuration");
                if (elemList != null && elemList.Count > 0)
                {
                    XmlElement elem = elemList[0] as XmlElement;
                    if (elem != null)
                    {
                        elemList = elem.GetElementsByTagName("feed-url");
                        if (elemList != null && elemList.Count > 0)
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
            Directory.CreateDirectory(InstalledPackagesPath);
            InterfaceVersion = string.Empty;
            if (LoadUpdaterConfig())
            {
                if (!string.IsNullOrEmpty(FeedUrl))
                {
                    try
                    {
                        using (Stream i = new FileStream(Path.Combine(InstalledPackagesPath, "SilverSim.Core.spkg"), FileMode.Open))
                        {
                            PackageDescription desc = new PackageDescription(i);
                            InterfaceVersion = desc.InterfaceVersion;
                        }
                    }
                    catch
                    {
                        /* if interface version is not set, we simply switch to disabled */
                        InterfaceVersion = string.Empty;
                    }
                }
            }
            foreach(string deletefile in Directory.GetFiles(Path.Combine(InstallRootPath, "bin"), "*.delete", SearchOption.AllDirectories))
            {
                File.Delete(deletefile);
            }
        }

        public void CheckForUpdates()
        {
            if(string.IsNullOrEmpty(InterfaceVersion))
            {
                return;
            }
            LoadInstalledPackageDescriptions();
            UpdatePackageFeed();
            List<string> updatable = new List<string>();
            foreach (KeyValuePair<string, PackageDescription> kvp in m_InstalledPackages)
            {
                PackageDescription current;
                if (m_AvailablePackages.TryGetValue(kvp.Key, out current) && current.Version != kvp.Value.Version)
                {
                    updatable.Add(kvp.Key);
                }
            }

            foreach(string pkg in updatable)
            {
                InstallPackage(pkg);
            }
        }

        public void LoadInstalledPackageDescriptions()
        {
            string[] pkgfiles = Directory.GetFiles(InstalledPackagesPath, "*.spkg");
            m_InstalledPackages.Clear();
            foreach (string pkgfile in pkgfiles)
            {
                using (Stream i = new FileStream(pkgfile, FileMode.Open))
                {
                    PackageDescription desc;
                    try
                    {
                        desc = new PackageDescription(i);
                    }
                    catch(Exception e)
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

        #region Package feed handling
        public void UpdatePackageFeed()
        {
            if(string.IsNullOrEmpty(InterfaceVersion))
            {
                /* debugging does not have any package data normally, so we skip loading the feed */
                return;
            }

            List<PackageDescription> updatesAvail = new List<PackageDescription>();
            foreach(KeyValuePair<string, PackageDescription> kvp in m_InstalledPackages)
            {
                PackageDescription current = new PackageDescription(FeedUrl + InterfaceVersion + "/" + kvp.Key + ".spkg");
                m_AvailablePackages[current.Name] = current;
            }
        }

        public bool AreUpdatesAvailable
        {
            get
            {
                if(m_AvailablePackages.Count == 0)
                {
                    UpdatePackageFeed();
                }
                foreach(KeyValuePair<string, PackageDescription> kvp in m_InstalledPackages)
                {
                    PackageDescription current;
                    if(m_AvailablePackages.TryGetValue(kvp.Key, out current) && current.Version != kvp.Value.Version)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        #endregion

        public void UninstallPackage(string packagename)
        {
            foreach(PackageDescription packsearch in m_InstalledPackages.Values)
            {
                if(packsearch.Dependencies.Keys.Contains(packagename))
                {
                    throw new PackageDependencyFoundException(packsearch.Name);
                }
            }

            PackageDescription pack = m_InstalledPackages[packagename];
            File.Delete(Path.Combine(InstalledPackagesPath, pack.Name + ".spkg"));
            foreach(KeyValuePair<string, PackageDescription.FileInfo> kvp in pack.Files)
            {
                File.Delete(kvp.Key);
            }
        }

        public void InstallPackage(string packagename)
        {
            Dictionary<string, PackageDescription> requiredPackages = new Dictionary<string, PackageDescription>();
            Dictionary<string, string> newDependencies = new Dictionary<string, string>();
            newDependencies.Add(packagename, string.Empty);
            while(newDependencies.Count != 0)
            {
                string pkg = newDependencies.Keys.First();
                string version = newDependencies[pkg];
                newDependencies.Remove(pkg);
                PackageDescription current;
                current = string.IsNullOrEmpty(version) ? 
                    new PackageDescription(FeedUrl + InterfaceVersion + "/" + pkg + ".spkg") :
                    new PackageDescription(FeedUrl + InterfaceVersion + "/" + version +  "/" + pkg + ".spkg");
                requiredPackages.Add(current.Name, current);
                foreach (KeyValuePair<string, string> dep in current.Dependencies)
                {
                    if (requiredPackages.ContainsKey(dep.Key) || 
                        (m_InstalledPackages.ContainsKey(dep.Key) && 
                        (string.IsNullOrEmpty(dep.Value) || dep.Value == m_InstalledPackages[dep.Key].Version)))
                    {
                        continue;
                    }
                    newDependencies.Add(dep.Key, dep.Value);
                }
            }

            foreach(PackageDescription package in requiredPackages.Values)
            {
                DownloadPackage(package);
                UnpackPackage(package);
            }
        }

        string GetCacheFileName(PackageDescription package)
        {
            return Path.Combine(PackageCachePath, package.InterfaceVersion + "-" + package.Version + "-" + package.Name + ".zip");
        }

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
            foreach (PackageDescription pack in m_InstalledPackages.Values)
            {
                if (!VerifyInstalledPackage(pack))
                {
                    DownloadPackage(pack);
                    UnpackPackage(pack);
                }
            }
        }

        bool VerifyInstalledPackage(PackageDescription pack)
        {
            foreach (KeyValuePair<string, PackageDescription.FileInfo> kvp in pack.Files)
            {
                if (kvp.Value.Hash != null)
                {
                    using (SHA256 hash = SHA256.Create())
                    {
                        using (FileStream fs = new FileStream(Path.Combine(InstallRootPath, kvp.Key), FileMode.Open, FileAccess.Read))
                        {
                            hash.ComputeHash(fs);
                        }

                        if (!hash.Hash.Equals(kvp.Value.Hash))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        #endregion

        void UnpackPackage(PackageDescription package)
        {
            string cachefile = GetCacheFileName(package);

            try
            {
                using (SHA256 hash = SHA256.Create())
                {
                    using (FileStream fs = new FileStream(cachefile, FileMode.Open, FileAccess.Read))
                    {
                        hash.ComputeHash(fs);
                    }

                    if (!hash.Hash.Equals(package.Hash))
                    {
                        throw new InvalidPackageHashException();
                    }
                }
            }
            catch
            {
                File.Delete(cachefile);
                throw;
            }

            if(m_InstalledPackages.ContainsKey(package.Name))
            {
                /* Delete files first */
                foreach(KeyValuePair<string, PackageDescription.FileInfo> kvp in m_InstalledPackages[package.Name].Files)
                {
                    File.Delete(kvp.Key);
                }
            }

            package.WriteFile(Path.Combine(InstalledPackagesPath, package.Name + ".spkg"));

            using (FileStream fs = new FileStream(cachefile, FileMode.Open))
            {
                using (ZipArchive zip = new ZipArchive(fs))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        using (Stream i = entry.Open())
                        {
                            string targetFile = Path.Combine(InstallRootPath, entry.FullName);
                            if (package.RequiresReplacement)
                            {
                                File.Move(targetFile, targetFile + ".delete");
                                IsRestartRequired = true;
                            }
                            using (FileStream o = new FileStream(targetFile, FileMode.Create))
                            {
                                i.CopyTo(o);
                            }
                        }
                    }
                }
            }
        }

        void DownloadPackage(PackageDescription package)
        {
            string cachefile = GetCacheFileName(package);
            if (!File.Exists(cachefile))
            {
                WebRequest webreq = WebRequest.Create(FeedUrl + package.InterfaceVersion + "/" + package.Version + "/" + package.Name + ".zip");
                try
                {
                    using (Stream s = webreq.GetRequestStream())
                    {
                        using (FileStream fs = new FileStream(cachefile, FileMode.Create))
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

            try
            { 
                using (SHA256 hash = SHA256.Create())
                {
                    using (FileStream fs = new FileStream(cachefile, FileMode.Open, FileAccess.Read))
                    {
                        hash.ComputeHash(fs);
                    }

                    if(!Array.Equals(hash.Hash, package.Hash))
                    {
                        throw new InvalidPackageHashException();
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
