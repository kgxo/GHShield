using System;
using System.Windows.Forms;

namespace GHShield.Managers
{
    public static class ContextMenuManager
    {
        public static void FreezeClicked(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Freeze clicked!",
                "GHShield",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}