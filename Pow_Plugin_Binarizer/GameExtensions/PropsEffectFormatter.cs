// 药品存档解析类
using System;
using MessagePack;
using MessagePack.Formatters;
using Heluo.Data;

namespace PathOfWuxia
{
    public sealed class PropsEffectFormatter : IMessagePackFormatter<PropsEffect>, IMessagePackFormatter
    {
        PropsEffect factory = new PropsEffect();

        public PropsEffect Create( string[] from )
        {
            string typestr = from[0].Trim();
            if (Enum.TryParse(typestr, out PropsEffectType type))
            {
                return factory.Create(type, from) as PropsEffect;
            }

            if (Enum.TryParse(typestr, out PropsEffectType_Ext typeEx))
            {
                switch (typeEx)
                {
                    case PropsEffectType_Ext.LearnSkill:
                        return new PropsLearnSkill(from[1].Trim());
                    case PropsEffectType_Ext.LearnMantra:
                        return new PropsLearnMantra(from[1].Trim());
                    case PropsEffectType_Ext.Reward:
                        return new PropsReward(from[1].Trim());
                    case PropsEffectType_Ext.Talk:
                        return new PropsTalk(from[1].Trim());
                }
            }

            return null;
        }

        public PropsEffect Deserialize(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            PropsEffect result;
            if (MessagePackBinary.IsNil(bytes, offset))
            {
                readSize = 1;
                result = null;
            }
            else
            {
                string s = MessagePackBinary.ReadString(bytes, offset, out readSize);
                string[] from = s.Substring(1, s.Length - 2).Split(new char[]
                {
                    ','
                });

                result = Create(from);
            }
            return result;
        }

        public int Serialize(ref byte[] bytes, int offset, PropsEffect value, IFormatterResolver formatterResolver)
        {
            int result;
            if (value == null)
            {
                result = MessagePackBinary.WriteNil(ref bytes, offset);
            }
            else
            {
                string str = value.ToText();
                result = MessagePackBinary.WriteString(ref bytes, offset, str);
            }
            return result;
        }
        public static readonly PropsEffectFormatter Instance = new PropsEffectFormatter();
    }

    public sealed class PropsEffectResolver : IFormatterResolver
    {
        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            if (typeof(T) == typeof(PropsEffect))
            {
                return PropsEffectFormatter.Instance as IMessagePackFormatter<T>;
            }
            return null;
        }
        public static readonly PropsEffectResolver Instance = new PropsEffectResolver();
    }
}