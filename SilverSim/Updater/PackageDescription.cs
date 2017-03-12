// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace SilverSim.Updater
{
    [Serializable]
    public class InvalidPackageDescriptionException : Exception
    {
        public InvalidPackageDescriptionException() { }
        public InvalidPackageDescriptionException(string message) : base(message) { }
        protected InvalidPackageDescriptionException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public InvalidPackageDescriptionException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class PackageDescription
    {
        public string Version { get; protected set; }
        public string InterfaceVersion { get; protected set; }
        public string Name { get; protected set; }
        public byte[] Hash { get; protected set; }
        public bool IsCheckedForUpdates { get; protected set; }
        protected readonly Dictionary<string, string> m_Dependencies = new Dictionary<string, string>();
        protected readonly Dictionary<string, FileInfo> m_Files = new Dictionary<string, FileInfo>();
        protected readonly List<Configuration> m_DefaultConfigurations = new List<Configuration>();
        public IReadOnlyDictionary<string, string> Dependencies { get { return m_Dependencies; } }
        public IReadOnlyDictionary<string, FileInfo> Files { get { return m_Files; } }
        public IReadOnlyList<Configuration> DefaultConfigurations { get { return m_DefaultConfigurations; } }

        public struct FileInfo
        {
            public byte[] Hash;
            public string Version;
        }

        public struct Configuration
        {
            public string Source;
            public IReadOnlyList<string> StartTypes;
        }

        protected PackageDescription()
        {

        }

        public PackageDescription(string url)
        {
            using (XmlTextReader reader = new XmlTextReader(url))
            {
                LoadPackageData(reader);
            }
        }

        public PackageDescription(Stream input)
        {
            using (XmlTextReader reader = new XmlTextReader(input))
            {
                LoadPackageData(reader);
            }
        }

        void LoadPackageData(XmlTextReader reader)
        {
            while(reader.Read())
            {
                switch(reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name == "package")
                        {
                            LoadPackageDataMain(reader);
                            return;
                        }
                        else
                        {
                            throw new InvalidPackageDescriptionException();
                        }

                    default:
                        break;
                }
            }
            throw new InvalidPackageDescriptionException();
        }

        void LoadPackageDataMain(XmlTextReader reader)
        {
            while(reader.Read())
            {
                switch(reader.NodeType)
                {
                    case XmlNodeType.Element:
                        switch(reader.Name)
                        {
                            case "version":
                                if(reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                Version = reader.ReadElementContentAsString();
                                break;

                            case "check-for-updates":
                                if(reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                IsCheckedForUpdates = reader.ReadElementContentAsBoolean();
                                break;

                            case "sha256":
                                if(reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                Hash = Convert.FromBase64String(reader.ReadElementContentAsString());
                                break;

                            case "default-configuration":
                                if(reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                m_DefaultConfigurations.Add(LoadPackageDataDefaultCfg(reader));
                                break;

                            case "interface-version":
                                if(reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                InterfaceVersion = reader.ReadElementContentAsString();
                                break;

                            case "name":
                                if(reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                Name = reader.ReadElementContentAsString();
                                break;

                            case "dependencies":
                                if(reader.IsEmptyElement)
                                {
                                    break;
                                }
                                LoadPackageDataDependencies(reader);
                                break;

                            case "files":
                                if (reader.IsEmptyElement)
                                {
                                    break;
                                }
                                LoadPackageDataFiles(reader);
                                break;

                            default:
                                if(!reader.IsEmptyElement)
                                {
                                    reader.Skip();
                                }
                                break;
                        }
                        break;

                    case XmlNodeType.EndElement:
                        if(reader.Name == "package")
                        {
                            return;
                        }
                        throw new InvalidPackageDescriptionException();
                }
            }
            throw new InvalidPackageDescriptionException();
        }

        Configuration LoadPackageDataDefaultCfg(XmlTextReader reader)
        {
            Configuration cfg = new Configuration();
            List<string> startTypes = new List<string>();
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        switch (reader.Name)
                        {
                            case "source":
                                if (reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                cfg.Source = reader.ReadElementContentAsString();
                                break;

                            case "use-if-started-as":
                                if (reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                startTypes.Add(reader.ReadElementContentAsString());
                                break;

                            default:
                                if (!reader.IsEmptyElement)
                                {
                                    reader.Skip();
                                }
                                break;
                        }
                        break;

                    case XmlNodeType.EndElement:
                        if (reader.Name == "default-configuration")
                        {
                            cfg.StartTypes = startTypes;
                            return cfg;
                        }
                        throw new InvalidPackageDescriptionException();
                }
            }
            throw new InvalidPackageDescriptionException();
        }

        void LoadPackageDataFiles(XmlTextReader reader)
        {
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        switch (reader.Name)
                        {
                            case "file":
                                FileInfo fi = new FileInfo();
                                string fname = string.Empty;

                                if (reader.MoveToFirstAttribute())
                                {
                                    do
                                    {
                                        switch (reader.Name)
                                        {
                                            case "name":
                                                fname = reader.Value;
                                                break;

                                            case "version":
                                                fi.Version = reader.Value;
                                                break;

                                            case "sha256":
                                                fi.Hash = Convert.FromBase64String(reader.Value);
                                                break;

                                            default:
                                                break;
                                        }
                                    }
                                    while (reader.MoveToNextAttribute());

                                    m_Files.Add(fname, fi);
                                }
                                if (!reader.IsEmptyElement)
                                {
                                    reader.Skip();
                                }
                                break;

                            default:
                                if (!reader.IsEmptyElement)
                                {
                                    reader.Skip();
                                }
                                break;
                        }
                        break;

                    case XmlNodeType.EndElement:
                        if (reader.Name == "files")
                        {
                            return;
                        }
                        throw new InvalidPackageDescriptionException();
                }
            }
            throw new InvalidPackageDescriptionException();
        }

        void LoadPackageDataDependencies(XmlTextReader reader)
        {
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        switch (reader.Name)
                        {
                            case "dependency":
                                string version = string.Empty;
                                string name = string.Empty;

                                if (reader.MoveToFirstAttribute())
                                {
                                    do
                                    {
                                        switch (reader.Name)
                                        {
                                            case "name":
                                                name = reader.Value;
                                                break;

                                            case "version":
                                                version = reader.Value;
                                                break;

                                            default:
                                                break;
                                        }
                                    }
                                    while (reader.MoveToNextAttribute());
                                    m_Dependencies.Add(name, version);
                                }
                                if(!reader.IsEmptyElement)
                                {
                                    reader.Skip();
                                }
                                break;

                            default:
                                if (!reader.IsEmptyElement)
                                {
                                    reader.Skip();
                                }
                                break;
                        }
                        break;

                    case XmlNodeType.EndElement:
                        if (reader.Name == "dependencies")
                        {
                            return;
                        }
                        throw new InvalidPackageDescriptionException();
                }
            }
            throw new InvalidPackageDescriptionException();
        }

        public void WriteFile(string filename)
        {
            using (Stream s = new FileStream(filename, FileMode.Create))
            {
                using (XmlTextWriter w = new XmlTextWriter(s, m_UTF8NoBOM))
                {
                    w.WriteStartElement("package");
                    {
                        w.WriteStartElement("name");
                        w.WriteValue(Name);
                        w.WriteEndElement();

                        w.WriteStartElement("version");
                        w.WriteValue(Version);
                        w.WriteEndElement();

                        w.WriteStartElement("check-for-updates");
                        w.WriteValue(IsCheckedForUpdates);
                        w.WriteEndElement();

                        w.WriteStartElement("interface-version");
                        w.WriteValue(InterfaceVersion);
                        w.WriteEndElement();

                        if(Hash != null)
                        {
                            w.WriteStartElement("sha256");
                            w.WriteValue(Convert.ToBase64String(Hash));
                            w.WriteEndElement();
                        }

                        foreach(Configuration cfg in m_DefaultConfigurations)
                        {
                            w.WriteStartElement("default-configuration");
                            w.WriteStartElement("source");
                            w.WriteValue(cfg.Source);
                            w.WriteEndElement();
                            foreach(string start in cfg.StartTypes)
                            {
                                w.WriteStartElement("use-if-started-as");
                                w.WriteValue(start);
                                w.WriteEndElement();
                            }
                            w.WriteEndElement();
                        }

                        if (m_Dependencies.Count != 0)
                        {
                            w.WriteStartElement("dependencies");
                            foreach(KeyValuePair<string, string> p in m_Dependencies)
                            {
                                w.WriteStartElement("dependency");
                                w.WriteAttributeString("name", p.Key);
                                if(!string.IsNullOrEmpty(p.Value))
                                {
                                    w.WriteAttributeString("version", p.Value);
                                }
                                w.WriteEndElement();
                            }
                            w.WriteEndElement();
                        }

                        if(m_Files.Count != 0)
                        {
                            w.WriteStartElement("files");
                            foreach(KeyValuePair<string, FileInfo> kvp in m_Files)
                            {
                                w.WriteStartElement("file");
                                w.WriteAttributeString("name", kvp.Key);
                                if (!string.IsNullOrEmpty(kvp.Value.Version))
                                {
                                    w.WriteAttributeString("version", kvp.Value.Version);
                                }
                                w.WriteAttributeString("sha256", Convert.ToBase64String(kvp.Value.Hash));
                            }
                            w.WriteEndElement();
                        }
                    }
                    w.WriteEndElement();
                }
            }
        }

        static UTF8Encoding m_UTF8NoBOM = new UTF8Encoding(false);
    }
}
