// SilverSim is distributed under the terms of the
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
using System.Linq;
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
        public string License { get; protected set; }
        public string Name { get; protected set; }
        public byte[] Hash { get; protected set; }
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
            License = string.Empty;
            InterfaceVersion = string.Empty;
            Version = string.Empty;
        }

        public PackageDescription(string url)
        {
            License = string.Empty;
            InterfaceVersion = string.Empty;
            Version = string.Empty;
            using (XmlTextReader reader = new XmlTextReader(url))
            {
                LoadPackageData(reader);
            }
        }

        public PackageDescription(Stream input)
        {
            License = string.Empty;
            InterfaceVersion = string.Empty;
            Version = string.Empty;
            using (XmlTextReader reader = new XmlTextReader(input))
            {
                LoadPackageData(reader);
            }
        }

        public byte[] FromHexStringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public string ToHexString(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", string.Empty);
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


        public string ReadElementValueAsString(XmlTextReader reader)
        {
            string tagname = reader.Name;
            if (reader.IsEmptyElement)
            {
                return string.Empty;
            }

            for (;;)
            {
                if (!reader.Read())
                {
                    throw new XmlException("Premature end of XML");
                }

                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        throw new XmlException("Unexpected child node");

                    case XmlNodeType.Text:
                        return reader.ReadContentAsString();

                    case XmlNodeType.EndElement:
                        if (reader.Name != tagname)
                        {
                            throw new XmlException("closing tag does not match");
                        }
                        return string.Empty;

                    default:
                        break;
                }
            }
        }

        public void ReadToEndElement(XmlTextReader reader, string tagname = null)
        {
            if (string.IsNullOrEmpty(tagname))
            {
                tagname = reader.Name;
            }
            XmlNodeType nodeType = reader.NodeType;
            if ((nodeType == XmlNodeType.Element || nodeType == XmlNodeType.Attribute) && !reader.IsEmptyElement)
            {
                do
                {
                    nextelem:
                    if (!reader.Read())
                    {
                        throw new XmlException("Premature end of XML", null, reader.LineNumber, reader.LinePosition);
                    }
                    nodeType = reader.NodeType;
                    if (nodeType == XmlNodeType.Element)
                    {
                        ReadToEndElement(reader);
                        goto nextelem;
                    }
                } while (nodeType != XmlNodeType.EndElement);
                if (tagname != reader.Name)
                {
                    throw new XmlException("Closing tag does not match", null, reader.LineNumber, reader.LinePosition);
                }
            }
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
                                Version = ReadElementValueAsString(reader);
                                break;

                            case "sha256":
                                if(reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                Hash = FromHexStringToByteArray(ReadElementValueAsString(reader));
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
                                InterfaceVersion = ReadElementValueAsString(reader);
                                break;

                            case "license":
                                if(reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                License = ReadElementValueAsString(reader);
                                break;

                            case "name":
                                if(reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                Name = ReadElementValueAsString(reader);
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
                                ReadToEndElement(reader);
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
                                cfg.Source = ReadElementValueAsString(reader);
                                break;

                            case "use-if-started-as":
                                if (reader.IsEmptyElement)
                                {
                                    throw new InvalidPackageDescriptionException();
                                }
                                startTypes.Add(ReadElementValueAsString(reader));
                                break;

                            default:
                                ReadToEndElement(reader);
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
                                bool isEmptyElement = reader.IsEmptyElement;

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
                                                fi.Hash = FromHexStringToByteArray(reader.Value);
                                                break;

                                            default:
                                                break;
                                        }
                                    }
                                    while (reader.MoveToNextAttribute());

                                    m_Files.Add(fname, fi);
                                }
                                if (!isEmptyElement)
                                {
                                    ReadToEndElement(reader, "file");
                                }
                                break;

                            default:
                                ReadToEndElement(reader);
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
                                bool isEmptyElement = reader.IsEmptyElement;

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
                                if(!isEmptyElement)
                                {
                                    ReadToEndElement(reader);
                                }
                                break;

                            default:
                                ReadToEndElement(reader);
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
            if(File.Exists(filename))
            {
                File.Delete(filename);
            }
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

                        w.WriteStartElement("interface-version");
                        w.WriteValue(InterfaceVersion);
                        w.WriteEndElement();

                        if (!string.IsNullOrEmpty(License))
                        {
                            w.WriteStartElement("license");
                            w.WriteValue(License);
                            w.WriteEndElement();
                        }

                        if (Hash != null)
                        {
                            w.WriteStartElement("sha256");
                            w.WriteValue(ToHexString(Hash));
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
                                w.WriteAttributeString("sha256", ToHexString(kvp.Value.Hash));
                                w.WriteEndElement();
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
