using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OpenKh.Command.AnbMaker.Models.XmlOps
{
    [XmlRoot("InterpolatedAnb", Namespace = Common.Namespace)]
    public class InterpolatedAnbElement
    {
        [XmlAttribute] public string? Name { get; set; }

        [XmlElement(Namespace = Common.Namespace)] public MotionHeaderElement? MotionHeader { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public InterpolatedMotionHeaderElement? InterpolatedMotionHeader { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public RootPositionElement? RootPosition { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public ConstraintActivationElement[]? ConstraintActivation { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public ConstraintElement[]? Constraint { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public ExpressionNodeElement[]? ExpressionNode { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public ExpressionElement[]? Expression { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public ExternalEffectorElement[]? ExternalEffector { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public FCurveKeyElement[]? FCurveKey { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public FCurveElement[]? FCurvesForward { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public FCurveElement[]? FCurvesInverse { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public IKHelperElement[]? IKHelper { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public InitialPoseElement[]? InitialPose { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public JointElement[]? Joint { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public KeyFloatElement[]? KeyTangent { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public KeyFloatElement[]? KeyTime { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public KeyFloatElement[]? KeyValue { get; set; }
        [XmlElement(Namespace = Common.Namespace)] public LimiterElement[]? Limiter { get; set; }
    }
}
