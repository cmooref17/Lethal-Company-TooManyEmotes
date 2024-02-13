using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TooManyEmotes.Compatibility
{
    public static class LCVR_Patcher
    {
        public static bool Enabled { get { return Plugin.IsModLoaded("io.daxcess.lcvr"); } }
    }
}
