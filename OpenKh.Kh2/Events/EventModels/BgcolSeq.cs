using System;
using System.Collections.Generic;
using System.Text;
using Xe.BinaryMapper;

namespace OpenKh.Kh2.Events.EventModels
{
    public class BgcolSeq : IEventObject
    {
        public static ushort Type => 12;

        [Data] public short start_frame { get; set; }
        [Data] public short end_frame { get; set; }
        [Data] public short put_id { get; set; }
        [Data] public short flag { get; set; }

    }
}