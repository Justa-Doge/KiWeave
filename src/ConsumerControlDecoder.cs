using System;

namespace FunctionRowRemapper
{
    internal static class ConsumerControlDecoder
    {
        internal static string NameFor(ushort page, ushort usage)
        {
            if (page == 0x0C && usage == 0xB8) return "MediaEject";
            return null;
        }
    }
}
