using System;
using System.Collections.Generic;
using System.Text;
using Xe.BinaryMapper;

namespace OpenKh.Kh2.Events.EventModels
{
    public class SeqMes : IEventObject
    {
        public static ushort Type => 21;

        [Data] public short start_frame { get; set; }
        [Data] public short put_id { get; set; }
        [Data] public short mes_no { get; set; }
        [Data] public short type { get; set; }

    }
}