using OpenKh.Tools.Common;
using OpenKh.Tools.ImgzViewer.Views;
using Xe.Tools;

namespace OpenKh.Tools.ImgdViewer
{
	public class ImgzModule : IToolModule<ToolInvokeDesc>
    {
		public bool? ShowDialog(ToolInvokeDesc desc)
		{
			return new ImgzView(desc).ShowDialog();
		}
	}
}
