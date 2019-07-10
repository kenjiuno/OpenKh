using OpenKh.Tools.Common;
using OpenKh.Tools.ImgdViewer.Views;
using Xe.Tools;

namespace OpenKh.Tools.ImgdViewer
{
	public class ImgdModule : IToolModule<ToolInvokeDesc>
    {
		public bool? ShowDialog(ToolInvokeDesc desc)
		{
			return new ImgdView(desc).ShowDialog();
		}
	}
}
