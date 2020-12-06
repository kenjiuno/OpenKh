using OpenKh.Common;
using OpenKh.Kh2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenKh.Engine.Motion
{
    public class Kh2MotionEngine : IMotionEngine
    {
        private readonly List<Bar.Entry> _animEntries;
        private int _animationIndex;
        private Kh2.Motion _motion;

        public Kh2MotionEngine(List<Bar.Entry> entries)
        {
            _animEntries = entries;
            _animationIndex = -1;
        }

        public int AnimationCount => _animEntries.Count;

        public int CurrentAnimationIndex
        {
            get => _animationIndex;
            set
            {
                _animationIndex = value;
                var entry = _animEntries[_animationIndex];
                var subEntries = Bar.Read(entry.Stream.SetPosition(0));
                var animationDataEntry = subEntries.FirstOrDefault(x => x.Type == Bar.EntryType.Motion);
                if (animationDataEntry != null)
                    _motion = Kh2.Motion.Read(animationDataEntry.Stream.SetPosition(0));
                else
                    Console.Error.WriteLine($"MSET animation {CurrentAnimationIndex} ({CurrentAnimationShortName}) does not contain any {Bar.EntryType.Motion}");

                var animationBinaryEntry = subEntries.FirstOrDefault(x => x.Type == Bar.EntryType.MotionTriggers);
                if (animationDataEntry != null)
                {
                    // We do not have any ANB parser
                }
                else
                    Console.Error.WriteLine($"MSET animation {CurrentAnimationIndex} ({CurrentAnimationShortName}) does not contain any {Bar.EntryType.MotionTriggers}");
            }
        }

        public string CurrentAnimationShortName =>
            _animEntries[_animationIndex].Name;

        public Kh2.Motion CurrentMotion => _motion;

        public void ApplyMotion(IModelMotion model, float time)
        {
            if (CurrentMotion.IsRaw)
                ApplyRawMotion(model, CurrentMotion.Raw, time);
            else
                ApplyInterpolatedMotion(model, CurrentMotion.Interpolated, time);
        }

        private void ApplyRawMotion(IModelMotion model, Kh2.Motion.RawMotion motion, float time)
        {
            var absoluteFrame = (int)Math.Floor(motion.FramePerSecond * time);
            var actualFrame = absoluteFrame % motion.Matrices.Count;
            model.ApplyMotion(motion.Matrices[actualFrame]);
        }

        public static void ApplyInterpolatedMotion(IModelMotion model, Kh2.Motion.InterpolatedMotion motion, float time)
        {
            // The original game engine seems to always rotate the model by 90 degrees, for some reason
            const float BaseRotation = (float)(Math.PI / 180 * 90);

            var boneList = model.Bones.ToArray();
            var matrices = new Matrix4x4[boneList.Length];
            var absTranslationList = new Vector3[matrices.Length];
            var absRotationList = new Quaternion[matrices.Length];

            for (int x = 0; x < matrices.Length; x++)
            {
                Quaternion absRotation;
                Vector3 absTranslation;
                var oneBone = boneList[x];
                var parent = oneBone.Parent;
                if (parent < 0)
                {
                    absRotation = Quaternion.Identity;
                    absTranslation = Vector3.Zero;
                }
                else
                {
                    absRotation = absRotationList[parent];
                    absTranslation = absTranslationList[parent];
                }

                var TranslationX = oneBone.TranslationX;
                var TranslationY = oneBone.TranslationY;
                var TranslationZ = oneBone.TranslationZ;

                var RotationX = oneBone.RotationX;
                var RotationY = oneBone.RotationY;
                var RotationZ = oneBone.RotationZ;

                motion.StaticPose.Where(it => it.BoneIndex == x).ToList().ForEach(
                    pose =>
                    {
                        switch (pose.Channel)
                        {
                            case 3:
                                RotationX = pose.Value;
                                break;
                            case 4:
                                RotationY = pose.Value;
                                break;
                            case 5:
                                RotationZ = pose.Value;
                                break;

                            case 6:
                                TranslationX = pose.Value;
                                break;
                            case 7:
                                TranslationY = pose.Value;
                                break;
                            case 8:
                                TranslationZ = pose.Value;
                                break;
                        }
                    }
                );

                var localTranslation = Vector3.Transform(new Vector3(TranslationX, TranslationY, TranslationZ), Matrix4x4.CreateFromQuaternion(absRotation));
                absTranslationList[x] = absTranslation + localTranslation;

                var localRotation = Quaternion.Identity;
                if (RotationZ != 0)
                    localRotation *= (Quaternion.CreateFromAxisAngle(Vector3.UnitZ, RotationZ));
                if (RotationY != 0)
                    localRotation *= (Quaternion.CreateFromAxisAngle(Vector3.UnitY, RotationY));
                if (RotationX != 0)
                    localRotation *= (Quaternion.CreateFromAxisAngle(Vector3.UnitX, RotationX));
                absRotationList[x] = absRotation * localRotation;
            }
            for (int x = 0; x < matrices.Length; x++)
            {
                var absMatrix = Matrix4x4.Identity;
                absMatrix *= Matrix4x4.CreateFromQuaternion(absRotationList[x]);
                absMatrix *= Matrix4x4.CreateTranslation(absTranslationList[x]);
                matrices[x] = absMatrix;
            }

            model.ApplyMotion(matrices);
        }
    }
}
