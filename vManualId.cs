using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BlzSoftTerminal
{
    public partial class vManualId : Form
    {
        public vManualId()
        {
            InitializeComponent();
        }

        private void txtID_KeyPress(object sender, KeyPressEventArgs e)
        {
            AllowNumber(sender, e);
        }

        private static void AllowNumber(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) )
            {
                e.Handled = true;
            }
            
        }

        private void btnIngresar_Click(object sender, EventArgs e)
        {
            Main.ID_detected = Convert.ToInt32(txtID.Text);
            Main.QRevent = true;
            this.Close();
        }

        private void teclaDigit_clic(object sender, EventArgs e)
        {
            Button tecla = (Button)sender;
            txtID.Text = txtID.Text + tecla.Tag;
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            string text = txtID.Text;

            if (text.Length < 1)
            { return; }
            txtID.Text = text.Remove(text.Length - 1, 1);
        }
    }
}
