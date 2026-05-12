using System;
using System.ComponentModel;
using System.Xml;
using System.Reflection;

namespace ScpControl
{
    public partial class XmlMapper : Component 
    {        
        public sealed record ControllerParseError(string Message, Exception? Exception = null)
        {
            public string ToDisplayString() => Message;

            public static ControllerParseError ValidationFailed(string message) => new(message);

            public static ControllerParseError Unknown(string message, Exception? ex = null) => new(message, ex);
        }

        public event EventHandler<DebugEventArgs>? Debug;

        protected virtual void LogDebug(String Data) 
        {
            DebugEventArgs args = new DebugEventArgs(Data);

            Debug?.Invoke(this, args);
        }

        protected Profile    m_Empty  = new Profile(true, DsMatch.None.ToString(), DsMatch.Global.ToString(), String.Empty);
        protected ProfileMap m_Mapper = new ProfileMap();

        protected Ds3ButtonAxisMap Ds3ButtonAxis = new Ds3ButtonAxisMap();
        protected Ds4ButtonAxisMap Ds4ButtonAxis = new Ds4ButtonAxisMap();

        protected volatile Boolean m_Remapping = false;
        protected volatile String  m_Active = String.Empty, m_Version = String.Empty, m_Description = String.Empty;

        protected Profile Find(String Mac, Int32 PadId) 
        {
            Profile Found = m_Empty;
            String  Pad   = ((DsPadId) PadId).ToString();

            DsMatch Current = DsMatch.None, Target = DsMatch.None;

            foreach(Profile Item in m_Mapper.Values)
            {
                Target = Item.Usage(Pad, Mac);

                if (Target > Current)
                {
                    Found = Item; Current = Target;
                }
            }

            return Found;
        }

        protected void CreateTextNode(XmlDocument Doc, XmlNode Node, String Name, String Text) 
        {
            XmlNode Item = Doc.CreateNode(XmlNodeType.Element, Name, null);

            if (Text.Length > 0)
            {
                XmlNode Elem = Doc.CreateNode(XmlNodeType.Text, Name, null);

                Elem.Value = Text;
                Item.AppendChild(Elem);
            }

            Node.AppendChild(Item);
        }

        private static bool TryReadText(XmlNode parent, string childName, bool required, string fallback, out string value, out ControllerParseError error)
        {
            value = fallback;
            error = null!;

            XmlNode? child = parent.SelectSingleNode(childName);
            if (child == null || child.FirstChild == null || child.FirstChild.Value == null)
            {
                if (required)
                {
                    error = ControllerParseError.ValidationFailed($"Missing required XML element '{childName}'.");
                    return false;
                }

                return true;
            }

            value = child.FirstChild.Value;
            return true;
        }

        private static bool TryParseEnumValue<TEnum>(string value, string description, out TEnum parsed, out ControllerParseError error)
            where TEnum : struct, Enum
        {
            if (Enum.TryParse<TEnum>(value, out TEnum parsedValue))
            {
                parsed = parsedValue;
                error = null!;
                return true;
            }

            parsed = default;
            error = ControllerParseError.ValidationFailed($"Invalid {description}: '{value}'.");
            return false;
        }


        public XmlMapper() 
        {
            InitializeComponent();

            Ds3ButtonAxis[Ds3Button.L1      ] = Ds3Axis.L1;
            Ds3ButtonAxis[Ds3Button.L2      ] = Ds3Axis.L2;
            Ds3ButtonAxis[Ds3Button.R1      ] = Ds3Axis.R1;
            Ds3ButtonAxis[Ds3Button.R2      ] = Ds3Axis.R2;

            Ds3ButtonAxis[Ds3Button.Triangle] = Ds3Axis.Triangle;
            Ds3ButtonAxis[Ds3Button.Circle  ] = Ds3Axis.Circle;
            Ds3ButtonAxis[Ds3Button.Cross   ] = Ds3Axis.Cross;
            Ds3ButtonAxis[Ds3Button.Square  ] = Ds3Axis.Square;

            Ds3ButtonAxis[Ds3Button.Up      ] = Ds3Axis.Up;
            Ds3ButtonAxis[Ds3Button.Right   ] = Ds3Axis.Right;
            Ds3ButtonAxis[Ds3Button.Down    ] = Ds3Axis.Down;
            Ds3ButtonAxis[Ds3Button.Left    ] = Ds3Axis.Left;

            Ds4ButtonAxis[Ds4Button.L2      ] = Ds4Axis.L2;
            Ds4ButtonAxis[Ds4Button.R2      ] = Ds4Axis.R2;
        }

        public XmlMapper(IContainer container) 
        {
            container.Add(this);

            InitializeComponent();

            Ds3ButtonAxis[Ds3Button.L1      ] = Ds3Axis.L1;
            Ds3ButtonAxis[Ds3Button.L2      ] = Ds3Axis.L2;
            Ds3ButtonAxis[Ds3Button.R1      ] = Ds3Axis.R1;
            Ds3ButtonAxis[Ds3Button.R2      ] = Ds3Axis.R2;

            Ds3ButtonAxis[Ds3Button.Triangle] = Ds3Axis.Triangle;
            Ds3ButtonAxis[Ds3Button.Circle  ] = Ds3Axis.Circle;
            Ds3ButtonAxis[Ds3Button.Cross   ] = Ds3Axis.Cross;
            Ds3ButtonAxis[Ds3Button.Square  ] = Ds3Axis.Square;

            Ds3ButtonAxis[Ds3Button.Up      ] = Ds3Axis.Up;
            Ds3ButtonAxis[Ds3Button.Right   ] = Ds3Axis.Right;
            Ds3ButtonAxis[Ds3Button.Down    ] = Ds3Axis.Down;
            Ds3ButtonAxis[Ds3Button.Left    ] = Ds3Axis.Left;

            Ds4ButtonAxis[Ds4Button.L2      ] = Ds4Axis.L2;
            Ds4ButtonAxis[Ds4Button.R2      ] = Ds4Axis.R2;
        }


        public virtual bool TryInitialize(XmlDocument Map, out ControllerParseError error)
        {
            error = null!;

            try
            {
                m_Remapping = false; m_Mapper.Clear();

                XmlNode? Node = Map.SelectSingleNode("/ScpMapper");
                if (Node is null)
                {
                    error = ControllerParseError.ValidationFailed("Missing required '/ScpMapper' root element.");
                    return false;
                }

                string description;
                if (!TryReadText(Node, "Description", false, String.Empty, out description, out error))
                    return false;
                m_Description = description;

                string version;
                if (!TryReadText(Node, "Version", false, String.Empty, out version, out error))
                    return false;
                m_Version = version;

                string active;
                if (!TryReadText(Node, "Active", false, String.Empty, out active, out error))
                    return false;
                m_Active = active;

                XmlNodeList? profileNodes = Node.SelectNodes("Mapping/Profile");
                if (profileNodes is null || profileNodes.Count == 0)
                {
                    error = ControllerParseError.ValidationFailed("No profile mappings were found.");
                    return false;
                }

                foreach (XmlNode ProfileNode in profileNodes)
                {
                    string Name;
                    if (!TryReadText(ProfileNode, "Name", true, String.Empty, out Name, out error))
                        return false;

                    string Type;
                    if (!TryReadText(ProfileNode, "Type", true, DsMatch.Global.ToString(), out Type, out error))
                        return false;

                    String Qualifier = String.Empty;

                    try
                    {
                        XmlNode? QualifierNode = ProfileNode.SelectSingleNode("Value");

                        if (QualifierNode != null && QualifierNode.HasChildNodes && QualifierNode.FirstChild != null && QualifierNode.FirstChild.Value != null)
                        {
                            Qualifier = QualifierNode.FirstChild.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        error = ControllerParseError.Unknown($"Failed reading profile qualifier for '{Name}'.", ex);
                        return false;
                    }

                    Profile Profile = new Profile(Name == m_Active, Name, Type, Qualifier);

                    XmlNode? ds3Buttons = ProfileNode.SelectSingleNode("DS3/Button");
                    if (ds3Buttons != null)
                    {
                        foreach (XmlNode mapping in ds3Buttons.ChildNodes)
                        {
                            if (mapping.NodeType != XmlNodeType.Element)
                                continue;

                            if (mapping.FirstChild == null || mapping.FirstChild.Value == null)
                            {
                                error = ControllerParseError.ValidationFailed($"DS3 button mapping '{mapping.Name}' is missing a value.");
                                return false;
                            }

                            Ds3Button target;
                            if (!TryParseEnumValue<Ds3Button>(mapping.Name, $"DS3 button target '{mapping.Name}'", out target, out error))
                                return false;

                            Ds3Button mapped;
                            if (!TryParseEnumValue<Ds3Button>(mapping.FirstChild.Value, $"DS3 button mapping for '{mapping.Name}'", out mapped, out error))
                                return false;

                            Profile.Ds3Button[target] = mapped;
                        }
                    }

                    XmlNode? ds3Axes = ProfileNode.SelectSingleNode("DS3/Axis");
                    if (ds3Axes != null)
                    {
                        foreach (XmlNode mapping in ds3Axes.ChildNodes)
                        {
                            if (mapping.NodeType != XmlNodeType.Element)
                                continue;

                            if (mapping.FirstChild == null || mapping.FirstChild.Value == null)
                            {
                                error = ControllerParseError.ValidationFailed($"DS3 axis mapping '{mapping.Name}' is missing a value.");
                                return false;
                            }

                            Ds3Axis target;
                            if (!TryParseEnumValue<Ds3Axis>(mapping.Name, $"DS3 axis target '{mapping.Name}'", out target, out error))
                                return false;

                            Ds3Axis mapped;
                            if (!TryParseEnumValue<Ds3Axis>(mapping.FirstChild.Value, $"DS3 axis mapping for '{mapping.Name}'", out mapped, out error))
                                return false;

                            Profile.Ds3Axis[target] = mapped;
                        }
                    }

                    XmlNode? ds4Buttons = ProfileNode.SelectSingleNode("DS4/Button");
                    if (ds4Buttons != null)
                    {
                        foreach (XmlNode mapping in ds4Buttons.ChildNodes)
                        {
                            if (mapping.NodeType != XmlNodeType.Element)
                                continue;

                            if (mapping.FirstChild == null || mapping.FirstChild.Value == null)
                            {
                                error = ControllerParseError.ValidationFailed($"DS4 button mapping '{mapping.Name}' is missing a value.");
                                return false;
                            }

                            Ds4Button target;
                            if (!TryParseEnumValue<Ds4Button>(mapping.Name, $"DS4 button target '{mapping.Name}'", out target, out error))
                                return false;

                            Ds4Button mapped;
                            if (!TryParseEnumValue<Ds4Button>(mapping.FirstChild.Value, $"DS4 button mapping for '{mapping.Name}'", out mapped, out error))
                                return false;

                            Profile.Ds4Button[target] = mapped;
                        }
                    }

                    XmlNode? ds4Axes = ProfileNode.SelectSingleNode("DS4/Axis");
                    if (ds4Axes != null)
                    {
                        foreach (XmlNode mapping in ds4Axes.ChildNodes)
                        {
                            if (mapping.NodeType != XmlNodeType.Element)
                                continue;

                            if (mapping.FirstChild == null || mapping.FirstChild.Value == null)
                            {
                                error = ControllerParseError.ValidationFailed($"DS4 axis mapping '{mapping.Name}' is missing a value.");
                                return false;
                            }

                            Ds4Axis target;
                            if (!TryParseEnumValue<Ds4Axis>(mapping.Name, $"DS4 axis target '{mapping.Name}'", out target, out error))
                                return false;

                            Ds4Axis mapped;
                            if (!TryParseEnumValue<Ds4Axis>(mapping.FirstChild.Value, $"DS4 axis mapping for '{mapping.Name}'", out mapped, out error))
                                return false;

                            Profile.Ds4Axis[target] = mapped;
                        }
                    }

                    m_Mapper[Profile.Name] = Profile;
                }

                Int32 Mappings = m_Mapper.TryGetValue(m_Active, out Profile? activeProfile) && activeProfile != null
                    ? activeProfile.Ds3Button.Count + activeProfile.Ds3Axis.Count + activeProfile.Ds4Button.Count + activeProfile.Ds4Axis.Count
                    : 0;
                LogDebug(String.Format("## Mapper.Initialize() - Profiles [{0}] Active [{1}] Mappings [{2}]", m_Mapper.Count, m_Active, Mappings));

                m_Remapping = true;
                error = null!;
                return true;
            }
            catch (Exception ex)
            {
                error = ControllerParseError.Unknown("Unexpected error while loading the mapper configuration.", ex);
                return false;
            }
        }

        public virtual Boolean Initialize(XmlDocument Map) 
        {
            return TryInitialize(Map, out _);
        }

        public virtual Boolean Shutdown() 
        {
            m_Remapping = false;

            LogDebug("## Mapper.Shutdown()");
            return true;
        }

        public virtual Boolean Construct(ref XmlDocument Map) 
        {
            Boolean Constructed = true;

            try
            {
                XmlNode     Node;
                XmlDocument Doc = new XmlDocument();

                Node = Doc.CreateXmlDeclaration("1.0", "utf-8", String.Empty);
                Doc.AppendChild(Node);

                Node = Doc.CreateComment(String.Format(" ScpMapper Configuration Data. {0} ", DateTime.Now));
                Doc.AppendChild(Node);

                Node = Doc.CreateNode(XmlNodeType.Element, "ScpMapper", null);
                {
                    CreateTextNode(Doc, Node, "Description", "SCP Mapping File");
                    CreateTextNode(Doc, Node, "Version",     Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? String.Empty);

                    XmlNode Mapping = Doc.CreateNode(XmlNodeType.Element, "Mapping", null);
                    {
                        foreach (Profile Item in m_Mapper.Values)
                        {
                            if (Item.Default) CreateTextNode(Doc, Node, "Active", Item.Name);

                            XmlNode Profile = Doc.CreateNode(XmlNodeType.Element, "Profile", null);
                            {
                                CreateTextNode(Doc, Profile, "Name",  Item.Name);
                                CreateTextNode(Doc, Profile, "Type",  Item.Type);
                                CreateTextNode(Doc, Profile, "Value", Item.Qualifier);

                                XmlNode Ds3 = Doc.CreateNode(XmlNodeType.Element, DsModel.DS3.ToString(), null);
                                {
                                    XmlNode Button = Doc.CreateNode(XmlNodeType.Element, "Button", null);
                                    {
                                        foreach (Ds3Button Ds3Button in Item.Ds3Button.Keys)
                                        {
                                            CreateTextNode(Doc, Button, Ds3Button.ToString(), Item.Ds3Button[Ds3Button].ToString());
                                        }
                                    }
                                    Ds3.AppendChild(Button);

                                    XmlNode Axis = Doc.CreateNode(XmlNodeType.Element, "Axis", null);
                                    {
                                        foreach (Ds3Axis Ds3Axis in Item.Ds3Axis.Keys)
                                        {
                                            CreateTextNode(Doc, Axis, Ds3Axis.ToString(), Item.Ds3Axis[Ds3Axis].ToString());
                                        }
                                    }
                                    Ds3.AppendChild(Axis);
                                }
                                Profile.AppendChild(Ds3);

                                XmlNode Ds4 = Doc.CreateNode(XmlNodeType.Element, DsModel.DS4.ToString(), null);
                                {
                                    XmlNode Button = Doc.CreateNode(XmlNodeType.Element, "Button", null);
                                    {
                                        foreach (Ds4Button Ds4Button in Item.Ds4Button.Keys)
                                        {
                                            CreateTextNode(Doc, Button, Ds4Button.ToString(), Item.Ds4Button[Ds4Button].ToString());
                                        }
                                    }
                                    Ds4.AppendChild(Button);

                                    XmlNode Axis = Doc.CreateNode(XmlNodeType.Element, "Axis", null);
                                    {
                                        foreach (Ds4Axis Ds4Axis in Item.Ds4Axis.Keys)
                                        {
                                            CreateTextNode(Doc, Axis, Ds4Axis.ToString(), Item.Ds4Axis[Ds4Axis].ToString());
                                        }
                                    }
                                    Ds4.AppendChild(Axis);
                                }
                                Profile.AppendChild(Ds4);
                            }
                            Mapping.AppendChild(Profile);
                        }
                    }
                    Node.AppendChild(Mapping);
                    
                }
                Doc.AppendChild(Node);

                Map = Doc;                
            }
            catch { Constructed = false; }

            return Constructed;
        }


        public virtual Boolean Remap(DsModel Type, Int32 Pad, String Mac, Byte[] Input, Byte[] Output) 
        {
            Boolean Mapped = false;

            try
            {
                if (m_Remapping)
                {
                    switch (Type)
                    {
                        case DsModel.DS3: Mapped = RemapDs3(Find(Mac, Pad), Input, Output); break;
                        case DsModel.DS4: Mapped = RemapDs4(Find(Mac, Pad), Input, Output); break;
                    }
                }
            }
            catch { }

            return Mapped;
        }


        public virtual Boolean RemapDs3(Profile Map, Byte[] Input, Byte[] Output) 
        {
            Boolean Mapped = false;

            try
            {
                Array.Copy(Input, Output, Input.Length);

                // Map Buttons
                Ds3Button In = (Ds3Button)(UInt32)((Input[10] << 0) | (Input[11] << 8) | (Input[12] << 16) | (Input[13] << 24));
                Ds3Button Out = In;

                foreach (Ds3Button Item in Map.Ds3Button.Keys) if ((Out & Item) != Ds3Button.None) Out ^= Item;
                foreach (Ds3Button Item in Map.Ds3Button.Keys) if ((In  & Item) != Ds3Button.None) Out |= Map.Ds3Button[Item];

                Output[10] = (Byte)((UInt32) Out >>  0 & 0xFF);
                Output[11] = (Byte)((UInt32) Out >>  8 & 0xFF);
                Output[12] = (Byte)((UInt32) Out >> 16 & 0xFF);
                Output[13] = (Byte)((UInt32) Out >> 24 & 0xFF);

                // Map Axis
                foreach (Ds3Axis Item in Map.Ds3Axis.Keys)
                {
                    switch (Item)
                    {
                        case Ds3Axis.LX:
                        case Ds3Axis.LY:
                        case Ds3Axis.RX:
                        case Ds3Axis.RY: 
                            Output[(UInt32) Item] = 127; // Centred
                            break;

                        default:
                            Output[(UInt32) Item] =   0;
                            break;
                    }
                }

                foreach (Ds3Axis Item in Map.Ds3Axis.Keys)
                {
                    if (Map.Ds3Axis[Item] != Ds3Axis.None)
                    {
                        Output[(UInt32) Map.Ds3Axis[Item]] = Input[(UInt32) Item];
                    }
                }

                // Fix up Button-Axis Relations
                foreach (Ds3Button Key in Ds3ButtonAxis.Keys)
                {
                    if ((Out & Key) != Ds3Button.None && Output[(UInt32) Ds3ButtonAxis[Key]] == 0)
                    {
                        Output[(UInt32) Ds3ButtonAxis[Key]] = 0xFF;
                    }
                }

                Mapped = true;
            }
            catch { }

            return Mapped;
        }

        public virtual Boolean RemapDs4(Profile Map, Byte[] Input, Byte[] Output) 
        {
            Boolean Mapped = false;

            try
            {
                Array.Copy(Input, Output, Input.Length);

                // Map Buttons
                Ds4Button In = (Ds4Button)(UInt32)((Input[13] << 0) | (Input[14] << 8) | (Input[15] << 16));
                Ds4Button Out = In;

                foreach (Ds4Button Item in Map.Ds4Button.Keys) if ((Out & Item) != Ds4Button.None) Out ^= Item;
                foreach (Ds4Button Item in Map.Ds4Button.Keys) if ((In  & Item) != Ds4Button.None) Out |= Map.Ds4Button[Item];

                Output[13] = (Byte)((UInt32) Out >>  0 & 0xFF);
                Output[14] = (Byte)((UInt32) Out >>  8 & 0xFF);
                Output[15] = (Byte)((UInt32) Out >> 16 & 0xFF);

                // Map Axis
                foreach (Ds4Axis Item in Map.Ds4Axis.Keys)
                {
                    switch (Item)
                    {
                        case Ds4Axis.LX:
                        case Ds4Axis.LY:
                        case Ds4Axis.RX:
                        case Ds4Axis.RY:
                            Output[(UInt32) Item] = 127; // Centred
                            break;
                        default:
                            Output[(UInt32) Item] = 0;
                            break;
                    }
                }

                foreach (Ds4Axis Item in Map.Ds4Axis.Keys)
                {
                    if (Map.Ds4Axis[Item] != Ds4Axis.None)
                    {
                        Output[(UInt32) Map.Ds4Axis[Item]] = Input[(UInt32) Item];
                    }
                }

                // Fix up Button-Axis Relations
                foreach (Ds4Button Key in Ds4ButtonAxis.Keys)
                {
                    if ((Out & Key) != Ds4Button.None && Output[(UInt32) Ds4ButtonAxis[Key]] == 0)
                    {
                        Output[(UInt32) Ds4ButtonAxis[Key]] = 0xFF;
                    }
                }


                Mapped = true;
            }
            catch { }

            return Mapped;
        }


        public virtual String[] Profiles 
        {
            get 
            {
                Int32 Index = 0;
                String[] List = new String[m_Mapper.Count];

                foreach (String Item in m_Mapper.Keys)
                {
                    List[Index++] = Item;
                }

                return List;
            }
        }

        public virtual String   Active   
        {
            get { return m_Active; }
        }

        public virtual ProfileMap Map    
        {
            get { return m_Mapper; }
        }
    }
}
