using System;

namespace InteropUtils
{
    /// <summary>
    /// Phobos Interop API 的统一返回值类型，遵循 COM 风格的 HRESULT 约定。
    /// 所有 Phobos Interop 导出函数均返回 HRESULT，使用 <see cref="Succeeded"/> / <see cref="Failed"/> 判定结果。
    /// </summary>
    /// <remarks>
    /// 与 Phobos 文档（docs/Interoperability.md）中的约定一致：
    /// <list type="table">
    ///   <listheader><term>HRESULT</term><description>含义</description></listheader>
    ///   <item><term><see cref="S_OK"/></term><description>操作成功完成。</description></item>
    ///   <item><term><see cref="S_FALSE"/></term><description>操作完成但无实际效果（例如未找到匹配的 AE）。</description></item>
    ///   <item><term><see cref="E_POINTER"/></term><description>必需的指针参数为空。</description></item>
    ///   <item><term><see cref="E_INVALIDARG"/></term><description>一个或多个参数无效。</description></item>
    ///   <item><term><see cref="E_UNEXPECTED"/></term><description>出现意外内部错误（例如扩展数据未找到）。</description></item>
    ///   <item><term><see cref="E_FAIL"/></term><description>操作失败。</description></item>
    /// </list>
    /// </remarks>
    public static class HResult
    {
        /// <summary>操作成功完成。</summary>
        public const int S_OK = 0;

        /// <summary>操作完成但无实际效果。</summary>
        public const int S_FALSE = 1;

        /// <summary>必需的指针参数为空。</summary>
        public const int E_POINTER = unchecked((int)0x80004003);

        /// <summary>一个或多个参数无效。</summary>
        public const int E_INVALIDARG = unchecked((int)0x80070057);

        /// <summary>出现意外内部错误。</summary>
        public const int E_UNEXPECTED = unchecked((int)0x8000FFFF);

        /// <summary>操作失败。</summary>
        public const int E_FAIL = unchecked((int)0x80004005);

        /// <summary>
        /// 判定 HRESULT 是否表示成功（包括 <see cref="S_OK"/> 与 <see cref="S_FALSE"/>）。
        /// 与 C++ 中 <c>SUCCEEDED(hr)</c> 宏语义一致。
        /// </summary>
        public static bool Succeeded(int hr) => hr >= 0;

        /// <summary>
        /// 判定 HRESULT 是否表示失败。
        /// 与 C++ 中 <c>FAILED(hr)</c> 宏语义一致。
        /// </summary>
        public static bool Failed(int hr) => hr < 0;
    }
}
