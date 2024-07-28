using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SshGame.Server.Packets
{
    internal abstract class ChannelRequest : IPacket<ChannelRequest>
    {
        public uint PacketSequence { get; set; }
        public PacketType Type { get => PacketType; }

        //   byte      SSH_MSG_CHANNEL_REQUEST
        //   uint32    recipient channel
        //   string    request type in US-ASCII characters only
        //   boolean   want reply
        //   ....      type-specific data follows
        public UInt32 RecipientChannel { get; set; }
        public abstract string RequestType { get; }
        public bool WantReply { get; set; }


        public static PacketType PacketType
        {
            get
            {
                return PacketType.SSH_MSG_CHANNEL_REQUEST;
            }
        }




        public static ChannelRequest Load(ByteReader reader)
        {
            var RecipientChannel = reader.GetUInt32();
            var RequestType = reader.GetString();
            var WantReply = reader.GetBoolean();

            if (RequestType == "pty-req")
            {
                Dictionary<TerminalModes, UInt32> Transform(ByteReader reader)
                {
                    var result = new Dictionary<TerminalModes, UInt32>();
                    var length = reader.GetUInt32();
                    var readed = 0;
                    while (readed < length)
                    {
                        var mode = (TerminalModes)reader.GetByte();
                        var parameter = reader.GetUInt32();
                        result[mode] = parameter;
                        readed += 5;
                    }
                    return result;
                }
                return new PtyRuquest()
                {
                    RecipientChannel = RecipientChannel,
                    WantReply = WantReply,
                    TermEnviroment = reader.GetString(),
                    TerminalCharacterWidth = reader.GetUInt32(),
                    TerminalCharacterHeight = reader.GetUInt32(),
                    TerminalPixelWidth = reader.GetUInt32(),
                    TerminalPixelHeight = reader.GetUInt32(),
                    EncodedTerminalModes = Transform(reader),
                };
            }
            else if (RequestType == "shell")
            {
                return new ShellRuquest()
                {
                    RecipientChannel = RecipientChannel,
                    WantReply = WantReply,
                };
            }
            else if (RequestType == "window-change")
            {
                return new WindowChange()
                {
                    RecipientChannel = RecipientChannel,
                    WantReply = WantReply,
                    TerminalCharacterWidth = reader.GetUInt32(),
                    TerminalCharacterHeight = reader.GetUInt32(),
                    TerminalPixelWidth = reader.GetUInt32(),
                    TerminalPixelHeight = reader.GetUInt32(),
                };
            }
            else if (RequestType == "env")
            {
                return new EnvironmentRuquest()
                {
                    RecipientChannel = RecipientChannel,
                    WantReply = WantReply,
                    Name = reader.GetString(),
                    Value = reader.GetString(),
                };
            }
            else
            {
                return new UnknownRuquest()
                {
                    RecipientChannel = RecipientChannel,
                    RequestTypeSet = RequestType,
                    WantReply = WantReply,
                };
            }


        }

        public virtual void InternalGetBytes(ByteWriter writer)
        {
            writer.WriteUInt32(RecipientChannel);
            writer.WriteString(RequestType);
            writer.WriteBoolean(WantReply);
        }

        public class UnknownRuquest : ChannelRequest
        {
            private string requestType;
            public override string RequestType { get => requestType; }
            public required string RequestTypeSet { set => requestType = value; }
        }
        public class ShellRuquest : ChannelRequest
        {
            public override string RequestType => "shell";

        }
        public class WindowChange : ChannelRequest
        {
            public override string RequestType => "window-change";
            public UInt32 TerminalCharacterWidth { get; set; }
            public UInt32 TerminalCharacterHeight { get; set; }
            public UInt32 TerminalPixelWidth { get; set; }
            public UInt32 TerminalPixelHeight { get; set; }

        }
        public class EnvironmentRuquest : ChannelRequest
        {
            public override string RequestType => "env";
            public required string Name { get; set; }
            public required string Value { get; set; }
            override public void InternalGetBytes(ByteWriter writer)
            {
                base.InternalGetBytes(writer);
                writer.WriteString(Name);
                writer.WriteString(Value);
            }
        }
        public class PtyRuquest : ChannelRequest
        {
            // string    TERM environment variable value (e.g., vt100)
            // uint32    terminal width, characters (e.g., 80)
            // uint32    terminal height, rows (e.g., 24)
            // uint32    terminal width, pixels (e.g., 640)
            // uint32    terminal height, pixels (e.g., 480)
            // string    encoded terminal modes
            public override string RequestType => "pty-req";

            public required string TermEnviroment { get; set; }
            public UInt32 TerminalCharacterWidth { get; set; }
            public UInt32 TerminalCharacterHeight { get; set; }
            public UInt32 TerminalPixelWidth { get; set; }
            public UInt32 TerminalPixelHeight { get; set; }
            public required Dictionary<TerminalModes, UInt32> EncodedTerminalModes { get; set; }
        }
    }

    internal enum TerminalModes
    {
    }
}
