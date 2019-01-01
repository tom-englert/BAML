// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable MemberCanBeProtected.Global
namespace Baml
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    using JetBrains.Annotations;

    internal struct BamlVersion
    {
        public ushort Major;
        public ushort Minor;
    }

    internal class BamlDocument
    {
        public BamlDocument(Stream bamlStream)
        {
            using (var rdr = new BinaryReader(bamlStream, Encoding.Unicode, true))
            {
                var len = rdr.ReadUInt32();

                Signature = new string(rdr.ReadChars((int)(len >> 1)));

                if (Signature != "MSBAML")
                    throw new NotSupportedException();

                rdr.ReadBytes((int)(((len + 3) & ~3) - len));
            }
        }

        [NotNull]
        public string Signature { get; }

        public BamlVersion ReaderVersion { get; set; }
        public BamlVersion UpdaterVersion { get; set; }
        public BamlVersion WriterVersion { get; set; }

        [NotNull, ItemNotNull]
        public IList<BamlRecord> Records { get; } = new List<BamlRecord>();
    }

    internal class BamlElement
    {
        public BamlElement([NotNull] BamlRecord header)
        {
            Header = header;
        }

        [NotNull]
        public BamlRecord Header { get; }
        [NotNull]
        public IList<BamlRecord> Body { get; } = new List<BamlRecord>();
        [NotNull]
        public IList<BamlElement> Children { get; } = new List<BamlElement>();

        public BamlElement Parent { get; private set; }
        public BamlRecord Footer { get; private set; }

        private static bool IsHeader([NotNull] BamlRecord rec)
        {
            switch (rec.Type)
            {
                case BamlRecordType.ConstructorParametersStart:
                case BamlRecordType.DocumentStart:
                case BamlRecordType.ElementStart:
                case BamlRecordType.KeyElementStart:
                case BamlRecordType.NamedElementStart:
                case BamlRecordType.PropertyArrayStart:
                case BamlRecordType.PropertyComplexStart:
                case BamlRecordType.PropertyDictionaryStart:
                case BamlRecordType.PropertyListStart:
                case BamlRecordType.StaticResourceStart:
                    return true;
            }
            return false;
        }

        private static bool IsFooter([NotNull] BamlRecord rec)
        {
            switch (rec.Type)
            {
                case BamlRecordType.ConstructorParametersEnd:
                case BamlRecordType.DocumentEnd:
                case BamlRecordType.ElementEnd:
                case BamlRecordType.KeyElementEnd:
                case BamlRecordType.PropertyArrayEnd:
                case BamlRecordType.PropertyComplexEnd:
                case BamlRecordType.PropertyDictionaryEnd:
                case BamlRecordType.PropertyListEnd:
                case BamlRecordType.StaticResourceEnd:
                    return true;
            }
            return false;
        }

        private static bool IsMatch([NotNull] BamlRecord header, [NotNull] BamlRecord footer)
        {
            switch (header.Type)
            {
                case BamlRecordType.ConstructorParametersStart:
                    return footer.Type == BamlRecordType.ConstructorParametersEnd;

                case BamlRecordType.DocumentStart:
                    return footer.Type == BamlRecordType.DocumentEnd;

                case BamlRecordType.KeyElementStart:
                    return footer.Type == BamlRecordType.KeyElementEnd;

                case BamlRecordType.PropertyArrayStart:
                    return footer.Type == BamlRecordType.PropertyArrayEnd;

                case BamlRecordType.PropertyComplexStart:
                    return footer.Type == BamlRecordType.PropertyComplexEnd;

                case BamlRecordType.PropertyDictionaryStart:
                    return footer.Type == BamlRecordType.PropertyDictionaryEnd;

                case BamlRecordType.PropertyListStart:
                    return footer.Type == BamlRecordType.PropertyListEnd;

                case BamlRecordType.StaticResourceStart:
                    return footer.Type == BamlRecordType.StaticResourceEnd;

                case BamlRecordType.ElementStart:
                case BamlRecordType.NamedElementStart:
                    return footer.Type == BamlRecordType.ElementEnd;

                default:
                    return false;
            }
        }

        public static BamlElement Read([NotNull] BamlDocument document)
        {
            var records = document.Records;

            Debug.Assert(records.Count > 0 && records[0].Type == BamlRecordType.DocumentStart);

            BamlElement current = null;
            var stack = new Stack<BamlElement>();

            foreach (var record in records)
            {
                if (IsHeader(record))
                {
                    var prev = current;

                    current = new BamlElement(record);

                    if (prev != null)
                    {
                        prev.Children.Add(current);
                        current.Parent = prev;
                        stack.Push(prev);
                    }
                }
                else if (IsFooter(record))
                {
                    if (current == null)
                        throw new Exception("Unexpected footer.");

                    // ReSharper disable once PossibleNullReferenceException
                    while (!IsMatch(current.Header, record))
                    {
                        // End record can be omitted (sometimes).
                        if (stack.Count > 0)
                            current = stack.Pop();
                    }
                    current.Footer = record;
                    if (stack.Count > 0)
                        current = stack.Pop();
                }
                else
                {
                    if (current == null)
                        throw new Exception("Unexpected record.");

                    current.Body.Add(record);
                }
            }

            Debug.Assert(stack.Count == 0);
            return current;
        }
    }

    internal enum BamlRecordType : byte
    {
        ClrEvent = 0x13,
        Comment = 0x17,
        AssemblyInfo = 0x1c,
        AttributeInfo = 0x1f,
        ConstructorParametersStart = 0x2a,
        ConstructorParametersEnd = 0x2b,
        ConstructorParameterType = 0x2c,
        ConnectionId = 0x2d,
        ContentProperty = 0x2e,
        DefAttribute = 0x19,
        DefAttributeKeyString = 0x26,
        DefAttributeKeyType = 0x27,
        DeferableContentStart = 0x25,
        DefTag = 0x18,
        DocumentEnd = 0x2,
        DocumentStart = 0x1,
        ElementEnd = 0x4,
        ElementStart = 0x3,
        EndAttributes = 0x1a,
        KeyElementEnd = 0x29,
        KeyElementStart = 0x28,
        LastRecordType = 0x39,
        LineNumberAndPosition = 0x35,
        LinePosition = 0x36,
        LiteralContent = 0xf,
        NamedElementStart = 0x2f,
        OptimizedStaticResource = 0x37,
        PIMapping = 0x1b,
        PresentationOptionsAttribute = 0x34,
        ProcessingInstruction = 0x16,
        Property = 0x5,
        PropertyArrayEnd = 0xa,
        PropertyArrayStart = 0x9,
        PropertyComplexEnd = 0x8,
        PropertyComplexStart = 0x7,
        PropertyCustom = 0x6,
        PropertyDictionaryEnd = 0xe,
        PropertyDictionaryStart = 0xd,
        PropertyListEnd = 0xc,
        PropertyListStart = 0xb,
        PropertyStringReference = 0x21,
        PropertyTypeReference = 0x22,
        PropertyWithConverter = 0x24,
        PropertyWithExtension = 0x23,
        PropertyWithStaticResourceId = 0x38,
        RoutedEvent = 0x12,
        StaticResourceEnd = 0x31,
        StaticResourceId = 0x32,
        StaticResourceStart = 0x30,
        StringInfo = 0x20,
        Text = 0x10,
        TextWithConverter = 0x11,
        TextWithId = 0x33,
        TypeInfo = 0x1d,
        TypeSerializerInfo = 0x1e,
        XmlAttribute = 0x15,
        XmlnsProperty = 0x14
    }

    internal abstract class BamlRecord
    {
        public abstract BamlRecordType Type { get; }
        public long Position { get; internal set; }
        public abstract void Read(BamlBinaryReader reader);
        public abstract void Write(BamlBinaryWriter writer);
    }

    internal abstract class SizedBamlRecord : BamlRecord
    {
        public override void Read([NotNull] BamlBinaryReader reader)
        {
            var pos = reader.BaseStream.Position;
            var size = reader.ReadEncodedInt();

            ReadData(reader, size - (int)(reader.BaseStream.Position - pos));
            Debug.Assert(reader.BaseStream.Position - pos == size);
        }

        private int SizeofEncodedInt(int val)
        {
            if ((val & ~0x7F) == 0)
            {
                return 1;
            }
            if ((val & ~0x3FFF) == 0)
            {
                return 2;
            }
            if ((val & ~0x1FFFFF) == 0)
            {
                return 3;
            }
            if ((val & ~0xFFFFFFF) == 0)
            {
                return 4;
            }
            return 5;
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            var pos = writer.BaseStream.Position;
            WriteData(writer);
            var size = (int)(writer.BaseStream.Position - pos);
            size = SizeofEncodedInt(SizeofEncodedInt(size) + size) + size;
            writer.BaseStream.Position = pos;
            writer.WriteEncodedInt(size);
            WriteData(writer);
        }

        protected abstract void ReadData(BamlBinaryReader reader, int size);
        protected abstract void WriteData(BamlBinaryWriter writer);
    }

    internal interface IBamlDeferRecord
    {
        void ReadDefer([NotNull, ItemNotNull] IList<BamlRecord> records, int index, [NotNull] Func<long, BamlRecord> resolve);
        void WriteDefer([NotNull, ItemNotNull] IList<BamlRecord> records, int index, [NotNull] BamlBinaryWriter wtr);
    }

    internal class XmlnsPropertyRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.XmlnsProperty;

        public string Prefix { get; set; }
        public string XmlNamespace { get; set; }
        [NotNull]
        public ushort[] AssemblyIds { get; set; } = Array.Empty<ushort>();

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            Prefix = reader.ReadString();
            XmlNamespace = reader.ReadString();
            AssemblyIds = new ushort[reader.ReadUInt16()];
            for (var i = 0; i < AssemblyIds.Length; i++)
                AssemblyIds[i] = reader.ReadUInt16();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(Prefix);
            writer.Write(XmlNamespace);
            writer.Write((ushort)AssemblyIds.Length);
            foreach (var i in AssemblyIds)
                writer.Write(i);
        }
    }

    internal class PresentationOptionsAttributeRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.PresentationOptionsAttribute;

        public string Value { get; set; }
        public ushort NameId { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            Value = reader.ReadString();
            NameId = reader.ReadUInt16();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(Value);
            writer.Write(NameId);
        }
    }

    internal class PIMappingRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.PIMapping;

        public string XmlNamespace { get; set; }
        public string ClrNamespace { get; set; }
        public ushort AssemblyId { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            XmlNamespace = reader.ReadString();
            ClrNamespace = reader.ReadString();
            AssemblyId = reader.ReadUInt16();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(XmlNamespace);
            writer.Write(ClrNamespace);
            writer.Write(AssemblyId);
        }
    }

    internal class AssemblyInfoRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.AssemblyInfo;

        public ushort AssemblyId { get; set; }
        public string AssemblyFullName { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            AssemblyId = reader.ReadUInt16();
            AssemblyFullName = reader.ReadString();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(AssemblyId);
            writer.Write(AssemblyFullName);
        }
    }

    internal class PropertyRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.Property;

        public ushort AttributeId { get; set; }
        public string Value { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            AttributeId = reader.ReadUInt16();
            Value = reader.ReadString();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(AttributeId);
            writer.Write(Value);
        }
    }

    internal class PropertyWithConverterRecord : PropertyRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyWithConverter;

        public ushort ConverterTypeId { get; set; }

        protected override void ReadData(BamlBinaryReader reader, int size)
        {
            base.ReadData(reader, size);
            ConverterTypeId = reader.ReadUInt16();
        }

        protected override void WriteData(BamlBinaryWriter writer)
        {
            base.WriteData(writer);
            writer.Write(ConverterTypeId);
        }
    }

    internal class PropertyCustomRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyCustom;

        public ushort AttributeId { get; set; }
        public ushort SerializerTypeId { get; set; }
        public byte[] Data { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            var pos = reader.BaseStream.Position;
            AttributeId = reader.ReadUInt16();
            SerializerTypeId = reader.ReadUInt16();
            Data = reader.ReadBytes(size - (int)(reader.BaseStream.Position - pos));
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(AttributeId);
            writer.Write(SerializerTypeId);
            writer.Write(Data);
        }
    }

    internal class DefAttributeRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.DefAttribute;

        public string Value { get; set; }
        public ushort NameId { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            Value = reader.ReadString();
            NameId = reader.ReadUInt16();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(Value);
            writer.Write(NameId);
        }
    }

    internal class DefAttributeKeyStringRecord : SizedBamlRecord, IBamlDeferRecord
    {
        internal uint pos = 0xffffffff;

        public override BamlRecordType Type => BamlRecordType.DefAttributeKeyString;

        public ushort ValueId { get; set; }
        public bool Shared { get; set; }
        public bool SharedSet { get; set; }

        public BamlRecord Record { get; set; }

        public void ReadDefer(IList<BamlRecord> records, int index, Func<long, BamlRecord> resolve)
        {
            var keys = true;
            do
            {
                // ReSharper disable once PossibleNullReferenceException
                switch (records[index].Type)
                {
                    case BamlRecordType.DefAttributeKeyString:
                    case BamlRecordType.DefAttributeKeyType:
                    case BamlRecordType.OptimizedStaticResource:
                        break;
                    case BamlRecordType.StaticResourceStart:
                        NavigateTree(records, BamlRecordType.StaticResourceStart, BamlRecordType.StaticResourceEnd, ref index);
                        break;
                    case BamlRecordType.KeyElementStart:
                        NavigateTree(records, BamlRecordType.KeyElementStart, BamlRecordType.KeyElementEnd, ref index);
                        break;
                    default:
                        keys = false;
                        index--;
                        break;
                }
                index++;
            } while (keys);

            // ReSharper disable once PossibleNullReferenceException
            Record = resolve(records[index].Position + pos);
        }

        public void WriteDefer(IList<BamlRecord> records, int index, BamlBinaryWriter wtr)
        {
            if (Record == null)
                throw new InvalidOperationException("Invalid record state");

            var keys = true;
            do
            {
                // ReSharper disable once PossibleNullReferenceException
                switch (records[index].Type)
                {
                    case BamlRecordType.DefAttributeKeyString:
                    case BamlRecordType.DefAttributeKeyType:
                    case BamlRecordType.OptimizedStaticResource:
                        break;
                    case BamlRecordType.StaticResourceStart:
                        NavigateTree(records, BamlRecordType.StaticResourceStart, BamlRecordType.StaticResourceEnd, ref index);
                        break;
                    case BamlRecordType.KeyElementStart:
                        NavigateTree(records, BamlRecordType.KeyElementStart, BamlRecordType.KeyElementEnd, ref index);
                        break;
                    default:
                        keys = false;
                        index--;
                        break;
                }
                index++;
            } while (keys);
            wtr.BaseStream.Seek(pos, SeekOrigin.Begin);
            // ReSharper disable once PossibleNullReferenceException
            wtr.Write((uint)(Record.Position - records[index].Position));
        }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            ValueId = reader.ReadUInt16();
            pos = reader.ReadUInt32();
            Shared = reader.ReadBoolean();
            SharedSet = reader.ReadBoolean();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(ValueId);
            pos = (uint)writer.BaseStream.Position;
            writer.Write((uint)0);
            writer.Write(Shared);
            writer.Write(SharedSet);
        }

        private static void NavigateTree([NotNull, ItemNotNull] IList<BamlRecord> records, BamlRecordType start, BamlRecordType end, ref int index)
        {
            index++;
            while (true) //Assume there always is a end
            {
                // ReSharper disable once PossibleNullReferenceException
                if (records[index].Type == start)
                    NavigateTree(records, start, end, ref index);
                else if (records[index].Type == end)
                    return;
                index++;
            }
        }
    }

    internal class TypeInfoRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.TypeInfo;

        public ushort TypeId { get; set; }
        public ushort AssemblyId { get; set; }
        public string TypeFullName { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            TypeId = reader.ReadUInt16();
            AssemblyId = reader.ReadUInt16();
            TypeFullName = reader.ReadString();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(TypeId);
            writer.Write(AssemblyId);
            writer.Write(TypeFullName);
        }
    }

    internal class TypeSerializerInfoRecord : TypeInfoRecord
    {
        public override BamlRecordType Type => BamlRecordType.TypeSerializerInfo;

        public ushort SerializerTypeId { get; set; }

        protected override void ReadData(BamlBinaryReader reader, int size)
        {
            base.ReadData(reader, size);
            SerializerTypeId = reader.ReadUInt16();
        }

        protected override void WriteData(BamlBinaryWriter writer)
        {
            base.WriteData(writer);
            writer.Write(SerializerTypeId);
        }
    }

    internal class AttributeInfoRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.AttributeInfo;

        public ushort AttributeId { get; set; }
        public ushort OwnerTypeId { get; set; }
        public byte AttributeUsage { get; set; }
        public string Name { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            AttributeId = reader.ReadUInt16();
            OwnerTypeId = reader.ReadUInt16();
            AttributeUsage = reader.ReadByte();
            Name = reader.ReadString();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(AttributeId);
            writer.Write(OwnerTypeId);
            writer.Write(AttributeUsage);
            writer.Write(Name);
        }
    }

    internal class StringInfoRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.StringInfo;

        public ushort StringId { get; set; }
        public string Value { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            StringId = reader.ReadUInt16();
            Value = reader.ReadString();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(StringId);
            writer.Write(Value);
        }
    }

    internal class TextRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.Text;

        public string Value { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            Value = reader.ReadString();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(Value);
        }
    }

    internal class TextWithConverterRecord : TextRecord
    {
        public override BamlRecordType Type => BamlRecordType.TextWithConverter;

        public ushort ConverterTypeId { get; set; }

        protected override void ReadData(BamlBinaryReader reader, int size)
        {
            base.ReadData(reader, size);
            ConverterTypeId = reader.ReadUInt16();
        }

        protected override void WriteData(BamlBinaryWriter writer)
        {
            base.WriteData(writer);
            writer.Write(ConverterTypeId);
        }
    }

    internal class TextWithIdRecord : TextRecord
    {
        public override BamlRecordType Type => BamlRecordType.TextWithId;

        public ushort ValueId { get; set; }

        protected override void ReadData(BamlBinaryReader reader, int size)
        {
            ValueId = reader.ReadUInt16();
        }

        protected override void WriteData(BamlBinaryWriter writer)
        {
            writer.Write(ValueId);
        }
    }

    internal class LiteralContentRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.LiteralContent;

        public string Value { get; set; }
        public uint Reserved0 { get; set; }
        public uint Reserved1 { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            Value = reader.ReadString();
            Reserved0 = reader.ReadUInt32();
            Reserved1 = reader.ReadUInt32();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(Value);
            writer.Write(Reserved0);
            writer.Write(Reserved1);
        }
    }

    internal class RoutedEventRecord : SizedBamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.RoutedEvent;

        public string Value { get; set; }
        public ushort AttributeId { get; set; }

        protected override void ReadData([NotNull] BamlBinaryReader reader, int size)
        {
            AttributeId = reader.ReadUInt16();
            Value = reader.ReadString();
        }

        protected override void WriteData([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(Value);
            writer.Write(AttributeId);
        }
    }

    internal class DocumentStartRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.DocumentStart;

        public bool LoadAsync { get; set; }
        public uint MaxAsyncRecords { get; set; }
        public bool DebugBaml { get; set; }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            LoadAsync = reader.ReadBoolean();
            MaxAsyncRecords = reader.ReadUInt32();
            DebugBaml = reader.ReadBoolean();
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(LoadAsync);
            writer.Write(MaxAsyncRecords);
            writer.Write(DebugBaml);
        }
    }

    internal class DocumentEndRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.DocumentEnd;

        public override void Read(BamlBinaryReader reader) { }
        public override void Write(BamlBinaryWriter writer) { }
    }

    internal class ElementStartRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.ElementStart;

        public ushort TypeId { get; set; }
        public byte Flags { get; set; }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            TypeId = reader.ReadUInt16();
            Flags = reader.ReadByte();
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(TypeId);
            writer.Write(Flags);
        }
    }

    internal class ElementEndRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.ElementEnd;

        public override void Read(BamlBinaryReader reader) { }
        public override void Write(BamlBinaryWriter writer) { }
    }

    internal class KeyElementStartRecord : DefAttributeKeyTypeRecord
    {
        public override BamlRecordType Type => BamlRecordType.KeyElementStart;
    }

    internal class KeyElementEndRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.KeyElementEnd;

        public override void Read(BamlBinaryReader reader) { }
        public override void Write(BamlBinaryWriter writer) { }
    }

    internal class ConnectionIdRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.ConnectionId;

        public uint ConnectionId { get; set; }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            ConnectionId = reader.ReadUInt32();
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(ConnectionId);
        }
    }

    internal class PropertyWithExtensionRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyWithExtension;

        public ushort AttributeId { get; set; }
        public ushort Flags { get; set; }
        public ushort ValueId { get; set; }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            AttributeId = reader.ReadUInt16();
            Flags = reader.ReadUInt16();
            ValueId = reader.ReadUInt16();
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(AttributeId);
            writer.Write(Flags);
            writer.Write(ValueId);
        }
    }

    internal class PropertyTypeReferenceRecord : PropertyComplexStartRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyTypeReference;

        public ushort TypeId { get; set; }

        public override void Read(BamlBinaryReader reader)
        {
            base.Read(reader);
            TypeId = reader.ReadUInt16();
        }

        public override void Write(BamlBinaryWriter writer)
        {
            base.Write(writer);
            writer.Write(TypeId);
        }
    }

    internal class PropertyStringReferenceRecord : PropertyComplexStartRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyStringReference;

        public ushort StringId { get; set; }

        public override void Read(BamlBinaryReader reader)
        {
            base.Read(reader);
            StringId = reader.ReadUInt16();
        }

        public override void Write(BamlBinaryWriter writer)
        {
            base.Write(writer);
            writer.Write(StringId);
        }
    }

    internal class PropertyWithStaticResourceIdRecord : StaticResourceIdRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyWithStaticResourceId;

        public ushort AttributeId { get; set; }

        public override void Read(BamlBinaryReader reader)
        {
            AttributeId = reader.ReadUInt16();
            base.Read(reader);
        }

        public override void Write(BamlBinaryWriter writer)
        {
            writer.Write(AttributeId);
            base.Write(writer);
        }
    }

    internal class ContentPropertyRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.ContentProperty;

        public ushort AttributeId { get; set; }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            AttributeId = reader.ReadUInt16();
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(AttributeId);
        }
    }

    internal class DefAttributeKeyTypeRecord : ElementStartRecord, IBamlDeferRecord
    {
        internal uint pos = 0xffffffff;

        public override BamlRecordType Type => BamlRecordType.DefAttributeKeyType;

        public bool Shared { get; set; }
        public bool SharedSet { get; set; }

        public BamlRecord Record { get; set; }

        public void ReadDefer(IList<BamlRecord> records, int index, Func<long, BamlRecord> resolve)
        {
            var keys = true;
            do
            {
                // ReSharper disable once PossibleNullReferenceException
                switch (records[index].Type)
                {
                    case BamlRecordType.DefAttributeKeyString:
                    case BamlRecordType.DefAttributeKeyType:
                    case BamlRecordType.OptimizedStaticResource:
                        break;
                    case BamlRecordType.StaticResourceStart:
                        NavigateTree(records, BamlRecordType.StaticResourceStart, BamlRecordType.StaticResourceEnd, ref index);
                        break;
                    case BamlRecordType.KeyElementStart:
                        NavigateTree(records, BamlRecordType.KeyElementStart, BamlRecordType.KeyElementEnd, ref index);
                        break;
                    default:
                        keys = false;
                        index--;
                        break;
                }
                index++;
            } while (keys);

            // ReSharper disable once PossibleNullReferenceException
            Record = resolve(records[index].Position + pos);
        }

        public void WriteDefer(IList<BamlRecord> records, int index, BamlBinaryWriter wtr)
        {
            if (Record == null)
                throw new InvalidOperationException("Invalid record state");

            var keys = true;
            do
            {
                // ReSharper disable once PossibleNullReferenceException
                switch (records[index].Type)
                {
                    case BamlRecordType.DefAttributeKeyString:
                    case BamlRecordType.DefAttributeKeyType:
                    case BamlRecordType.OptimizedStaticResource:
                        break;
                    case BamlRecordType.StaticResourceStart:
                        NavigateTree(records, BamlRecordType.StaticResourceStart, BamlRecordType.StaticResourceEnd, ref index);
                        break;
                    case BamlRecordType.KeyElementStart:
                        NavigateTree(records, BamlRecordType.KeyElementStart, BamlRecordType.KeyElementEnd, ref index);
                        break;
                    default:
                        keys = false;
                        index--;
                        break;
                }
                index++;
            } while (keys);
            wtr.BaseStream.Seek(pos, SeekOrigin.Begin);
            // ReSharper disable once PossibleNullReferenceException
            wtr.Write((uint)(Record.Position - records[index].Position));
        }

        public override void Read(BamlBinaryReader reader)
        {
            base.Read(reader);
            pos = reader.ReadUInt32();
            Shared = reader.ReadBoolean();
            SharedSet = reader.ReadBoolean();
        }

        public override void Write(BamlBinaryWriter writer)
        {
            base.Write(writer);
            pos = (uint)writer.BaseStream.Position;
            writer.Write((uint)0);
            writer.Write(Shared);
            writer.Write(SharedSet);
        }

        private static void NavigateTree([NotNull, ItemNotNull] IList<BamlRecord> records, BamlRecordType start, BamlRecordType end, ref int index)
        {
            index++;
            while (true)
            {
                // ReSharper disable once PossibleNullReferenceException
                if (records[index].Type == start)
                    NavigateTree(records, start, end, ref index);
                else if (records[index].Type == end)
                    return;
                index++;
            }
        }
    }

    internal class PropertyListStartRecord : PropertyComplexStartRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyListStart;
    }

    internal class PropertyListEndRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyListEnd;

        public override void Read(BamlBinaryReader reader) { }
        public override void Write(BamlBinaryWriter writer) { }
    }

    internal class PropertyDictionaryStartRecord : PropertyComplexStartRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyDictionaryStart;
    }

    internal class PropertyDictionaryEndRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyDictionaryEnd;

        public override void Read(BamlBinaryReader reader) { }
        public override void Write(BamlBinaryWriter writer) { }
    }

    internal class PropertyArrayStartRecord : PropertyComplexStartRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyArrayStart;
    }

    internal class PropertyArrayEndRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyArrayEnd;

        public override void Read(BamlBinaryReader reader) { }
        public override void Write(BamlBinaryWriter writer) { }
    }

    internal class PropertyComplexStartRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyComplexStart;

        public ushort AttributeId { get; set; }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            AttributeId = reader.ReadUInt16();
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(AttributeId);
        }
    }

    internal class PropertyComplexEndRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.PropertyComplexEnd;

        public override void Read(BamlBinaryReader reader) { }
        public override void Write(BamlBinaryWriter writer) { }
    }

    internal class ConstructorParametersStartRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.ConstructorParametersStart;

        public override void Read(BamlBinaryReader reader) { }
        public override void Write(BamlBinaryWriter writer) { }
    }

    internal class ConstructorParametersEndRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.ConstructorParametersEnd;

        public override void Read(BamlBinaryReader reader) { }
        public override void Write(BamlBinaryWriter writer) { }
    }

    internal class ConstructorParameterTypeRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.ConstructorParameterType;

        public ushort TypeId { get; set; }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            TypeId = reader.ReadUInt16();
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(TypeId);
        }
    }

    internal class DeferableContentStartRecord : BamlRecord, IBamlDeferRecord
    {
        private long pos;
        internal uint size = 0xffffffff;

        public override BamlRecordType Type => BamlRecordType.DeferableContentStart;

        public BamlRecord Record { get; set; }

        public void ReadDefer(IList<BamlRecord> records, int index, Func<long, BamlRecord> resolve)
        {
            Record = resolve(pos + size);
        }

        public void WriteDefer(IList<BamlRecord> records, int index, BamlBinaryWriter wtr)
        {
            if (Record == null)
                throw new InvalidOperationException("Invalid record state");

            wtr.BaseStream.Seek(pos, SeekOrigin.Begin);
            wtr.Write((uint)(Record.Position - (pos + 4)));
        }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            size = reader.ReadUInt32();
            pos = reader.BaseStream.Position;
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            pos = writer.BaseStream.Position;
            writer.Write((uint)0);
        }
    }

    internal class StaticResourceStartRecord : ElementStartRecord
    {
        public override BamlRecordType Type => BamlRecordType.StaticResourceStart;
    }

    internal class StaticResourceEndRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.StaticResourceEnd;

        public override void Read(BamlBinaryReader reader) { }
        public override void Write(BamlBinaryWriter writer) { }
    }

    internal class StaticResourceIdRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.StaticResourceId;

        public ushort StaticResourceId { get; set; }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            StaticResourceId = reader.ReadUInt16();
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(StaticResourceId);
        }
    }

    internal class OptimizedStaticResourceRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.OptimizedStaticResource;

        public byte Flags { get; set; }
        public ushort ValueId { get; set; }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            Flags = reader.ReadByte();
            ValueId = reader.ReadUInt16();
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(Flags);
            writer.Write(ValueId);
        }
    }

    internal class LineNumberAndPositionRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.LineNumberAndPosition;

        public uint LineNumber { get; set; }
        public uint LinePosition { get; set; }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            LineNumber = reader.ReadUInt32();
            LinePosition = reader.ReadUInt32();
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(LineNumber);
            writer.Write(LinePosition);
        }
    }

    internal class LinePositionRecord : BamlRecord
    {
        public override BamlRecordType Type => BamlRecordType.LinePosition;

        public uint LinePosition { get; set; }

        public override void Read([NotNull] BamlBinaryReader reader)
        {
            LinePosition = reader.ReadUInt32();
        }

        public override void Write([NotNull] BamlBinaryWriter writer)
        {
            writer.Write(LinePosition);
        }
    }

    internal class NamedElementStartRecord : ElementStartRecord
    {
        public override BamlRecordType Type => BamlRecordType.NamedElementStart;

        public string RuntimeName { get; set; }

        public override void Read(BamlBinaryReader reader)
        {
            TypeId = reader.ReadUInt16();
            RuntimeName = reader.ReadString();
        }

        public override void Write(BamlBinaryWriter writer)
        {
            writer.Write(TypeId);
            if (RuntimeName != null)
            {
                writer.Write(RuntimeName);
            }
        }
    }

    internal class BamlBinaryReader : BinaryReader
    {
        public BamlBinaryReader([NotNull] Stream stream)
            : base(stream)
        {
        }

        [NotNull]
        // ReSharper disable once AssignNullToNotNullAttribute
        public override Stream BaseStream => base.BaseStream;

        public int ReadEncodedInt()
        {
            return Read7BitEncodedInt();
        }
    }

    internal class BamlBinaryWriter : BinaryWriter
    {
        public BamlBinaryWriter(Stream stream)
            : base(stream)
        {
        }

        [NotNull]
        // ReSharper disable once AssignNullToNotNullAttribute
        public override Stream BaseStream => base.BaseStream;

        public void WriteEncodedInt(int val)
        {
            Write7BitEncodedInt(val);
        }
    }

    internal static class Baml
    {
        [NotNull]
        public static BamlDocument ReadDocument([NotNull] Stream stream)
        {
            var reader = new BamlBinaryReader(stream);

            var document = new BamlDocument(reader.BaseStream)
            {
                ReaderVersion = new BamlVersion { Major = reader.ReadUInt16(), Minor = reader.ReadUInt16() },
                UpdaterVersion = new BamlVersion { Major = reader.ReadUInt16(), Minor = reader.ReadUInt16() },
                WriterVersion = new BamlVersion { Major = reader.ReadUInt16(), Minor = reader.ReadUInt16() }
            };

            if (document.ReaderVersion.Major != 0 || document.ReaderVersion.Minor != 0x60 ||
                document.UpdaterVersion.Major != 0 || document.UpdaterVersion.Minor != 0x60 ||
                document.WriterVersion.Major != 0 || document.WriterVersion.Minor != 0x60)
            {
                throw new NotSupportedException();
            }

            var records = document.Records;
            var recordsByIndex = new Dictionary<long, BamlRecord>();

            while (stream.Position < stream.Length)
            {
                var pos = stream.Position;
                var type = (BamlRecordType)reader.ReadByte();
                BamlRecord record;
                switch (type)
                {
                    case BamlRecordType.AssemblyInfo:
                        record = new AssemblyInfoRecord();
                        break;
                    case BamlRecordType.AttributeInfo:
                        record = new AttributeInfoRecord();
                        break;
                    case BamlRecordType.ConstructorParametersStart:
                        record = new ConstructorParametersStartRecord();
                        break;
                    case BamlRecordType.ConstructorParametersEnd:
                        record = new ConstructorParametersEndRecord();
                        break;
                    case BamlRecordType.ConstructorParameterType:
                        record = new ConstructorParameterTypeRecord();
                        break;
                    case BamlRecordType.ConnectionId:
                        record = new ConnectionIdRecord();
                        break;
                    case BamlRecordType.ContentProperty:
                        record = new ContentPropertyRecord();
                        break;
                    case BamlRecordType.DefAttribute:
                        record = new DefAttributeRecord();
                        break;
                    case BamlRecordType.DefAttributeKeyString:
                        record = new DefAttributeKeyStringRecord();
                        break;
                    case BamlRecordType.DefAttributeKeyType:
                        record = new DefAttributeKeyTypeRecord();
                        break;
                    case BamlRecordType.DeferableContentStart:
                        record = new DeferableContentStartRecord();
                        break;
                    case BamlRecordType.DocumentEnd:
                        record = new DocumentEndRecord();
                        break;
                    case BamlRecordType.DocumentStart:
                        record = new DocumentStartRecord();
                        break;
                    case BamlRecordType.ElementEnd:
                        record = new ElementEndRecord();
                        break;
                    case BamlRecordType.ElementStart:
                        record = new ElementStartRecord();
                        break;
                    case BamlRecordType.KeyElementEnd:
                        record = new KeyElementEndRecord();
                        break;
                    case BamlRecordType.KeyElementStart:
                        record = new KeyElementStartRecord();
                        break;
                    case BamlRecordType.LineNumberAndPosition:
                        record = new LineNumberAndPositionRecord();
                        break;
                    case BamlRecordType.LinePosition:
                        record = new LinePositionRecord();
                        break;
                    case BamlRecordType.LiteralContent:
                        record = new LiteralContentRecord();
                        break;
                    case BamlRecordType.NamedElementStart:
                        record = new NamedElementStartRecord();
                        break;
                    case BamlRecordType.OptimizedStaticResource:
                        record = new OptimizedStaticResourceRecord();
                        break;
                    case BamlRecordType.PIMapping:
                        record = new PIMappingRecord();
                        break;
                    case BamlRecordType.PresentationOptionsAttribute:
                        record = new PresentationOptionsAttributeRecord();
                        break;
                    case BamlRecordType.Property:
                        record = new PropertyRecord();
                        break;
                    case BamlRecordType.PropertyArrayEnd:
                        record = new PropertyArrayEndRecord();
                        break;
                    case BamlRecordType.PropertyArrayStart:
                        record = new PropertyArrayStartRecord();
                        break;
                    case BamlRecordType.PropertyComplexEnd:
                        record = new PropertyComplexEndRecord();
                        break;
                    case BamlRecordType.PropertyComplexStart:
                        record = new PropertyComplexStartRecord();
                        break;
                    case BamlRecordType.PropertyCustom:
                        record = new PropertyCustomRecord();
                        break;
                    case BamlRecordType.PropertyDictionaryEnd:
                        record = new PropertyDictionaryEndRecord();
                        break;
                    case BamlRecordType.PropertyDictionaryStart:
                        record = new PropertyDictionaryStartRecord();
                        break;
                    case BamlRecordType.PropertyListEnd:
                        record = new PropertyListEndRecord();
                        break;
                    case BamlRecordType.PropertyListStart:
                        record = new PropertyListStartRecord();
                        break;
                    case BamlRecordType.PropertyStringReference:
                        record = new PropertyStringReferenceRecord();
                        break;
                    case BamlRecordType.PropertyTypeReference:
                        record = new PropertyTypeReferenceRecord();
                        break;
                    case BamlRecordType.PropertyWithConverter:
                        record = new PropertyWithConverterRecord();
                        break;
                    case BamlRecordType.PropertyWithExtension:
                        record = new PropertyWithExtensionRecord();
                        break;
                    case BamlRecordType.PropertyWithStaticResourceId:
                        record = new PropertyWithStaticResourceIdRecord();
                        break;
                    case BamlRecordType.RoutedEvent:
                        record = new RoutedEventRecord();
                        break;
                    case BamlRecordType.StaticResourceEnd:
                        record = new StaticResourceEndRecord();
                        break;
                    case BamlRecordType.StaticResourceId:
                        record = new StaticResourceIdRecord();
                        break;
                    case BamlRecordType.StaticResourceStart:
                        record = new StaticResourceStartRecord();
                        break;
                    case BamlRecordType.StringInfo:
                        record = new StringInfoRecord();
                        break;
                    case BamlRecordType.Text:
                        record = new TextRecord();
                        break;
                    case BamlRecordType.TextWithConverter:
                        record = new TextWithConverterRecord();
                        break;
                    case BamlRecordType.TextWithId:
                        record = new TextWithIdRecord();
                        break;
                    case BamlRecordType.TypeInfo:
                        record = new TypeInfoRecord();
                        break;
                    case BamlRecordType.TypeSerializerInfo:
                        record = new TypeSerializerInfoRecord();
                        break;
                    case BamlRecordType.XmlnsProperty:
                        record = new XmlnsPropertyRecord();
                        break;
                    //case BamlRecordType.XmlAttribute:
                    //case BamlRecordType.ProcessingInstruction:
                    //case BamlRecordType.LastRecordType:
                    //case BamlRecordType.EndAttributes:
                    //case BamlRecordType.DefTag:
                    //case BamlRecordType.ClrEvent:
                    //case BamlRecordType.Comment:
                    default:
                        throw new NotSupportedException();
                }

                record.Position = pos;
                record.Read(reader);
                records.Add(record);
                recordsByIndex.Add(pos, record);
            }

            for (var i = 0; i < records.Count; i++)
            {
                if (records[i] is IBamlDeferRecord defer)
                {
                    defer.ReadDefer(records, i, key => recordsByIndex[key]);
                }
            }

            return document;
        }

        public static void WriteDocument([NotNull] BamlDocument document, [NotNull] Stream stream)
        {
            var writer = new BamlBinaryWriter(stream);
            {
                var wtr = new BinaryWriter(stream, Encoding.Unicode);
                var len = document.Signature.Length * 2;
                wtr.Write(len);
                wtr.Write(document.Signature.ToCharArray());
                wtr.Write(new byte[((len + 3) & ~3) - len]);
            }
            writer.Write(document.ReaderVersion.Major);
            writer.Write(document.ReaderVersion.Minor);
            writer.Write(document.UpdaterVersion.Major);
            writer.Write(document.UpdaterVersion.Minor);
            writer.Write(document.WriterVersion.Major);
            writer.Write(document.WriterVersion.Minor);

            var defers = new List<KeyValuePair<int, IBamlDeferRecord>>();

            var records = document.Records;

            for (var i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                rec.Position = stream.Position;
                writer.Write((byte)rec.Type);
                rec.Write(writer);

                if (rec is IBamlDeferRecord deferRecord)
                {
                    defers.Add(new KeyValuePair<int, IBamlDeferRecord>(i, deferRecord));
                }
            }

            foreach (var defer in defers)
            {
                // ReSharper disable once PossibleNullReferenceException
                defer.Value.WriteDefer(records, defer.Key, writer);
            }
        }
    }
}