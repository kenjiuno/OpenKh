using OpenKh.Engine.Maths;

namespace OpenKh.Engine.Parsers.Kddf2
{
    public class ImmutableMesh
    {
        public IndexAssignment[] indexAssignmentList = null;
        public VertexAssignment[][] vertexAssignmentsList = null;
        public int textureIndex = -1;

        public int cntVertexMixer;
        public int cntSkip;
        public int cntVerticesMix2ToOne;
        public int cntVerticesMix3ToOne;
        public int cntVerticesMix4ToOne;
        public int cntVerticesMix5ToOne;
        public int cntVerticesMix6ToOne;
        public int cntVerticesMix7ToOne;
    }
}
