using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xe.BinaryMapper;
using static OpenKh.Kh2.Motion;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    public class InterpolatedMotionHeaderElement
    {
        [XmlAttribute] public short BoneCount { get; set; }
        [XmlAttribute] public short TotalBoneCount { get; set; }
        [XmlAttribute] public int FrameCount { get; set; }
        [XmlAttribute] public int IKHelperOffset { get; set; }
        [XmlAttribute] public int JointOffset { get; set; }
        [XmlAttribute] public int KeyTimeCount { get; set; }
        [XmlAttribute] public int InitialPoseOffset { get; set; }
        [XmlAttribute] public int InitialPoseCount { get; set; }
        [XmlAttribute] public int RootPositionOffset { get; set; }
        [XmlAttribute] public int FCurveForwardOffset { get; set; }
        [XmlAttribute] public int FCurveForwardCount { get; set; }
        [XmlAttribute] public int FCurveInverseOffset { get; set; }
        [XmlAttribute] public int FCurveInverseCount { get; set; }
        [XmlAttribute] public int FCurveKeyOffset { get; set; }
        [XmlAttribute] public int KeyTimeOffset { get; set; }
        [XmlAttribute] public int KeyValueOffset { get; set; }
        [XmlAttribute] public int KeyTangentOffset { get; set; }
        [XmlAttribute] public int ConstraintOffset { get; set; }
        [XmlAttribute] public int ConstraintCount { get; set; }
        [XmlAttribute] public int ConstraintActivationOffset { get; set; }
        [XmlAttribute] public int LimiterOffset { get; set; }
        [XmlAttribute] public int ExpressionOffset { get; set; }
        [XmlAttribute] public int ExpressionCount { get; set; }
        [XmlAttribute] public int ExpressionNodeOffset { get; set; }
        [XmlAttribute] public int ExpressionNodeCount { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public BoundingBoxElement? BoundingBox { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public FrameDataElement? FrameData { get; set; }
        [XmlAttribute] public int ExternalEffectorOffset { get; set; }
        [XmlAttribute] public string? Reserved { get; set; }
    }
}
