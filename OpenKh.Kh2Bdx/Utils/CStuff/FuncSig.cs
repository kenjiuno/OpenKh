namespace OpenKh.Kh2Bdx.Utils.CStuff
{
    /// <summary>
    /// Callable function signature. Either a function defined in C source (jal) or a built-in trap (syscall).
    /// </summary>
    /// <param name="Name">function name</param>
    /// <param name="RetType">return type. void means no value on tp after the call.</param>
    /// <param name="ParamTypes">parameter types. null for syscall (types are unknown, only count is known).</param>
    /// <param name="NumIns">number of arguments</param>
    /// <param name="Syscall">(table, index) for a trap, null for local function</param>
    internal record FuncSig(
        string Name,
        TypeDesc RetType,
        TypeDesc[]? ParamTypes,
        int NumIns,
        (int Table, int Index)? Syscall = null);
}
