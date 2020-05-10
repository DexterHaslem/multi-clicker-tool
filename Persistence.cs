using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace multi_clicker_tool
{
    [System.SerializableAttribute]
    public class Settings
    {
        public int HotKey { get; set; }
        public ClickRepeatType RepeatType { get; set; }
        public int RepeatCount { get; set; }
        public int DelayMs { get; set; }
        public bool HumanizeDelay { get; set; }
        public bool HumanizeClickSpot { get; set; }
    }

    [System.SerializableAttribute]
    public class Persistence
    { 
        public Settings Settings { get; set; }
        public SavedClick[] Clicks { get; set; }


        public  string Serialize()
        {
            try
            {
                var xmlserializer = new XmlSerializer(typeof(Persistence));
                var stringWriter = new StringWriter();
                using (var writer = XmlWriter.Create(stringWriter))
                {
                    xmlserializer.Serialize(writer, this);
                    return stringWriter.ToString();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to serialize persistence", ex);
            }
        }

        public static Persistence Deserialize(string xmlFile)
        {
            if (string.IsNullOrWhiteSpace(xmlFile))
            {
                return null;
            }

            XmlSerializer serializer = new XmlSerializer(typeof(Persistence));

            using (StreamReader reader = new StreamReader(xmlFile))
            {
                Persistence p = (Persistence)serializer.Deserialize(reader);
                return p;
            }
        }
    }

}
