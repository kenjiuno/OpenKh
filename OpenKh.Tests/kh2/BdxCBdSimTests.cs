using OpenKh.BdSim;
using System.IO;
using Xunit;

namespace OpenKh.Tests.kh2
{
    /// <summary>
    /// Runs the C programs in kh2/res/bdx-c-e2e on bdSim (OpenKh.BdSim), the KH2 AI VM inside the test process, and
    /// compares what they print with NAME.expected: the same expectations as the bdEmu tests (BdxCEmuTests), but
    /// without bdEmu.exe and MobileAiExec.bin, so these always run.
    ///
    /// Each program defines `main`, which runs as trigger 1 after trigger 0 (OBJ_INIT, the global initializers).
    /// </summary>
    public class BdxCBdSimTests
    {
        public static System.Collections.Generic.IEnumerable<object[]> GetPrograms() => BdxCE2e.GetPrograms();

        [Theory]
        [MemberData(nameof(GetPrograms))]
        public void ProgramPrintsWhatIsExpected(string name)
        {
            var bdx = BdxCE2e.CompileToBdx(name);

            var vm = new BdVm();
            var init = vm.Run(bdx, trigger: 0, push: new[] { 0 }, keepLastState: false);
            var main = vm.Run(bdx, trigger: 1, push: null, keepLastState: true);

            Assert.Equal(BdStatus.Ret, init.Status);
            Assert.Equal(BdStatus.Ret, main.Status);
            Assert.Equal(BdxCE2e.Expected(name), main.Log.Trim());
        }
    }
}
