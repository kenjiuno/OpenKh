using Assimp;
using OpenKh.Command.AnbMaker.Models.XmlOps;
using OpenKh.Kh2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using YamlDotNet.Core.Tokens;
using static OpenKh.Kh2.Motion;

namespace OpenKh.Command.AnbMaker.Utils.XmlOps
{
    public class MsetXmlOps
    {
        public InterpolatedAnbElement ToXml(InterpolatedMotion source, string name)
        {
            var imh = source.InterpolatedMotionHeader;
            var mh = source.MotionHeader;
            var rp = source.RootPosition;

            return new InterpolatedAnbElement
            {
                Name = name,

                ConstraintActivation = source.ConstraintActivations
                    .Select(
                        (one, index) =>
                        {
                            return new ConstraintActivationElement
                            {
                                I = index,
                                Active = one.Active,
                                Time = one.Time,
                            };
                        }
                    )
                    .ToArray(),

                Constraint = source.Constraints
                    .Select(
                        (one, index) =>
                        {
                            return new ConstraintElement
                            {
                                I = index,
                                ActivationCount = one.ActivationCount,
                                ActivationStartId = one.ActivationStartId,
                                ConstrainedJointId = one.ConstrainedJointId,
                                LimiterId = one.LimiterId,
                                SourceJointId = one.SourceJointId,
                                TemporaryActiveFlag = one.TemporaryActiveFlag,
                                Type = one.Type,
                            };
                        }
                    )
                    .ToArray(),

                ExpressionNode = source.ExpressionNodes
                    .Select(
                        (one, index) =>
                        {
                            return new ExpressionNodeElement
                            {
                                I = index,
                                Value = one.Value,
                                CAR = one.CAR,
                                CDR = one.CDR,
                                Element = one.Element,
                                Type = one.Type,
                                IsGlobal = one.IsGlobal,
                            };
                        }
                    )
                    .ToArray(),

                Expression = source.Expressions
                    .Select(
                        (one, index) =>
                        {
                            return new ExpressionElement
                            {
                                I = index,
                                TargetId = one.TargetId,
                                TargetChannel = one.TargetChannel,
                                Reserved = one.Reserved,
                                NodeId = one.NodeId,
                            };
                        }
                    )
                    .ToArray(),

                ExternalEffector = source.ExternalEffectors
                    .Select(
                        (one, index) =>
                        {
                            return new ExternalEffectorElement
                            {
                                I = index,
                                JointId = one.JointId,
                            };
                        }
                    )
                    .ToArray(),

                FCurveKey = source.FCurveKeys
                    .Select(
                        (one, index) =>
                        {
                            return new FCurveKeyElement
                            {
                                I = index,
                                Type = (int)one.Type,
                                Time = one.Time,
                                ValueId = one.ValueId,
                                LeftTangentId = one.LeftTangentId,
                                RightTangentId = one.RightTangentId,
                            };
                        }
                    )
                    .ToArray(),

                FCurvesForward = source.FCurvesForward
                    .Select(ToFCurveElement)
                    .ToArray(),

                FCurvesInverse = source.FCurvesInverse
                    .Select(ToFCurveElement)
                    .ToArray(),

                IKHelper = source.IKHelpers
                    .Select(
                        (one, index) =>
                        {
                            return new IKHelperElement
                            {
                                I = index,
                                Index = one.Index,
                                SiblingId = one.SiblingId,
                                ParentId = one.ParentId,
                                ChildId = one.ChildId,
                                Reserved = one.Reserved,
                                Unknown = one.Unknown,
                                Terminate = one.Terminate,
                                Below = one.Below,
                                EnableBias = one.EnableBias,
                                ScaleX = one.ScaleX,
                                ScaleY = one.ScaleY,
                                ScaleZ = one.ScaleZ,
                                ScaleW = one.ScaleW,
                                RotateX = one.RotateX,
                                RotateY = one.RotateY,
                                RotateZ = one.RotateZ,
                                RotateW = one.RotateW,
                                TranslateX = one.TranslateX,
                                TranslateY = one.TranslateY,
                                TranslateZ = one.TranslateZ,
                                TranslateW = one.TranslateW,
                            };
                        }
                    )
                    .ToArray(),

                InitialPose = source.InitialPoses
                    .Select(
                        (one, index) =>
                        {
                            return new InitialPoseElement
                            {
                                I = index,
                                BoneId = one.BoneId,
                                ChannelValue = (int)one.ChannelValue,
                                Value = one.Value,
                            };
                        }
                    )
                    .ToArray(),

                InterpolatedMotionHeader = new InterpolatedMotionHeaderElement
                {
                    BoneCount = imh.BoneCount,
                    TotalBoneCount = imh.TotalBoneCount,
                    FrameCount = imh.FrameCount,
                    IKHelperOffset = imh.IKHelperOffset,
                    JointOffset = imh.JointOffset,
                    KeyTimeCount = imh.KeyTimeCount,
                    InitialPoseOffset = imh.InitialPoseOffset,
                    InitialPoseCount = imh.InitialPoseCount,
                    RootPositionOffset = imh.RootPositionOffset,
                    FCurveForwardOffset = imh.FCurveForwardOffset,
                    FCurveForwardCount = imh.FCurveForwardCount,
                    FCurveInverseOffset = imh.FCurveInverseOffset,
                    FCurveInverseCount = imh.FCurveInverseCount,
                    FCurveKeyOffset = imh.FCurveKeyOffset,
                    KeyTimeOffset = imh.KeyTimeOffset,
                    KeyValueOffset = imh.KeyValueOffset,
                    KeyTangentOffset = imh.KeyTangentOffset,
                    ConstraintOffset = imh.ConstraintOffset,
                    ConstraintCount = imh.ConstraintCount,
                    ConstraintActivationOffset = imh.ConstraintActivationOffset,
                    LimiterOffset = imh.LimiterOffset,
                    ExpressionOffset = imh.ExpressionOffset,
                    ExpressionCount = imh.ExpressionCount,
                    ExpressionNodeOffset = imh.ExpressionNodeOffset,
                    ExpressionNodeCount = imh.ExpressionNodeCount,
                    BoundingBox = new BoundingBoxElement
                    {
                        MaxX = imh.BoundingBox.BoundingBoxMaxX,
                        MaxY = imh.BoundingBox.BoundingBoxMaxY,
                        MaxZ = imh.BoundingBox.BoundingBoxMaxZ,
                        MaxW = imh.BoundingBox.BoundingBoxMaxW,
                        MinX = imh.BoundingBox.BoundingBoxMinX,
                        MinY = imh.BoundingBox.BoundingBoxMinY,
                        MinZ = imh.BoundingBox.BoundingBoxMinZ,
                        MinW = imh.BoundingBox.BoundingBoxMinW,
                    },
                    FrameData = new FrameDataElement
                    {
                        FrameStart = imh.FrameData.FrameStart,
                        FramesPerSecond = imh.FrameData.FramesPerSecond,
                        FrameEnd = imh.FrameData.FrameEnd,
                        FrameReturn = imh.FrameData.FrameReturn,
                    },
                    ExternalEffectorOffset = imh.ExternalEffectorOffset,
                    Reserved = IntArrayToString(imh.Reserved),
                },

                Joint = source.Joints
                    .Select(
                        (one, index) =>
                        {
                            return new JointElement
                            {
                                I = index,
                                JointId = one.JointId,
                                IK = one.IK,
                                ExtEffector = one.ExtEffector,
                                CalcMatrix2Rot = one.CalcMatrix2Rot,
                                Calculated = one.Calculated,
                                Fixed = one.Fixed,
                                Rotation = one.Rotation,
                                Trans = one.Trans,
                                Reserved = one.Reserved,
                            };
                        }
                    )
                    .ToArray(),

                KeyTangent = source.KeyTangents
                    .Select(
                        (one, index) =>
                        {
                            return new KeyFloatElement
                            {
                                I = index,
                                Value = one,
                            };
                        }
                    )
                    .ToArray(),

                KeyTime = source.KeyTimes
                    .Select(
                        (one, index) =>
                        {
                            return new KeyFloatElement
                            {
                                I = index,
                                Value = one,
                            };
                        }
                    )
                    .ToArray(),

                KeyValue = source.KeyValues
                    .Select(
                        (one, index) =>
                        {
                            return new KeyFloatElement
                            {
                                I = index,
                                Value = one,
                            };
                        }
                    )
                    .ToArray(),

                Limiter = source.Limiters
                    .Select(
                        (one, index) =>
                        {
                            return new LimiterElement
                            {
                                I = index,
                                Type = (int)one.Type,
                                HasXMin = one.HasXMin,
                                HasXMax = one.HasXMax,
                                HasYMin = one.HasYMin,
                                HasYMax = one.HasYMax,
                                HasZMin = one.HasZMin,
                                HasZMax = one.HasZMax,
                                Global = one.Global,
                                DampingWidth = one.DampingWidth,
                                DampingStrength = one.DampingStrength,
                                MinX = one.MinX,
                                MinY = one.MinY,
                                MinZ = one.MinZ,
                                MinW = one.MinW,
                                MaxX = one.MaxX,
                                MaxY = one.MaxY,
                                MaxZ = one.MaxZ,
                                MaxW = one.MaxW,
                            };
                        }
                    )
                    .ToArray(),

                MotionHeader = new MotionHeaderElement
                {
                    Type = mh.Type,
                    SubType = mh.SubType,
                    ExtraOffset = mh.ExtraOffset,
                    ExtraSize = mh.ExtraSize,
                },

                RootPosition = new RootPositionElement
                {
                    ScaleX = rp.ScaleX,
                    ScaleY = rp.ScaleY,
                    ScaleZ = rp.ScaleZ,
                    NotUnit = rp.NotUnit,
                    RotateX = rp.RotateX,
                    RotateY = rp.RotateY,
                    RotateZ = rp.RotateZ,
                    RotateW = rp.RotateW,
                    TranslateX = rp.TranslateX,
                    TranslateY = rp.TranslateY,
                    TranslateZ = rp.TranslateZ,
                    TranslateW = rp.TranslateW,
                    FCurveId = IntArrayToString(rp.FCurveId),
                },

            };
        }

        private string IntArrayToString(int[] reserved)
        {
            return string.Join(",", reserved);
        }

        private FCurveElement ToFCurveElement(Motion.FCurve one, int index)
        {
            return new FCurveElement
            {
                I = index,
                JointId = one.JointId,
                ChannelValue = (int)one.ChannelValue,
                Pre = (int)one.Pre,
                Post = (int)one.Post,
                KeyCount = one.KeyCount,
                KeyStartId = one.KeyStartId,
            };
        }
    }
}
