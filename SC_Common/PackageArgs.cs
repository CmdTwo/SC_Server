using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SC_Common.Enum;

namespace SC_Common
{
    [Serializable]
    public struct PackageArgs
    {
        public PackageType PackageType;
        public Command Command;
        public Event Event;
        public Dictionary<Argument, object> Arguments;
    }
}
