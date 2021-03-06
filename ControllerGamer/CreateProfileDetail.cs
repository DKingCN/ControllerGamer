﻿using ControllerGamer.Libraries;
using ControllerGamer.Libraries.Controllers;
using ControllerGamer.Libraries.ProfileLoader;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using ControllerGamer.Libraries.SimInput;
using ControllerGamer.Libraries.NetListener;

namespace ControllerGamer
{
    public delegate void LogWindowHandler(string msg);

    internal partial class CreateProfileDetail : Form
    {

        public static string IconPath;
        public static ProfileConfig Config = new ProfileConfig();
        public static Profile SelectedProfile;

        private static List<KeyValuePair<Profile, List<int>>> Running_Mapping = new List<KeyValuePair<Profile, List<int>>>();

        private FileSystemWatcher fileSystemWatcher1 = new FileSystemWatcher();


        public CreateProfileDetail()
        {
            InitializeComponent();

            Logger.LogUpdate += new LogWindowHandler(OnLogUpdate);


        }

        private void CreateProfileDetail_Load(object sender, EventArgs e)
        {
            LoadControllerToList();
            LoadProfileToList();
        }

        private void OnLogUpdate(string msg)
        {
            richTextBox_log.Invoke(new Action(() => richTextBox_log.Text += msg));
        }

        private void LoadControllerToList()
        {
            Controllers.Clear();
            checkedListBox_controllerlist.Items.Clear();
            if (Controllers.LoadControllers() > 0)
                for (int i = 0; i < Controllers.Count; i++)
                {
                    checkedListBox_controllerlist.Items.Add(Controllers.Get(i).GetProductName(),false);
                }
        }

        private void LoadProfileToList()
        {

            listViewProfile.Items.Clear();
            imageList1.Images.Clear();

            CleanText();

            listViewProfile.SmallImageList = imageList1;

            List<Profile> profiles = ProfileManager.LoadProfiles();
            for (int i=0; i < profiles.Count; i++)
            {
                Profile profile = profiles[i];
                FileStream fs = new FileStream(@"Profiles\" + profile.Config.ProfileID + @"\" + profile.Config.IconFileName, FileMode.Open, FileAccess.Read);
                int byteLength = (int)fs.Length;
                byte[] fileBytes = new byte[byteLength];
                fs.Read(fileBytes, 0, byteLength);
                fs.Close();
                Image img = Image.FromStream(new MemoryStream(fileBytes));
                imageList1.Images.Add(img);

                ListViewItem item = new ListViewItem();
                item.Text = profile.Config.ProfileName;
                item.Tag = profile.Config.ProfileID;
                item.ImageIndex = i;
                item.ToolTipText = profile.Config.ProfileID;
                listViewProfile.Items.Add(item);
            }
            if (listViewProfile.Items.Count > 0)
            {
                listViewProfile.Items[0].Selected = true;
                listViewProfile.Items[0].EnsureVisible();
            }
        }



        private void buttonSaveCurrent_Click(object sender, EventArgs e)
        {
            DumpValues();
            ProfileManager.SaveProfile(isCreatingNew: false);
            LoadProfileToList();
        }

        private void listViewProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewProfile.SelectedItems.Count > 0)
            {
                ListViewItem item = listViewProfile.SelectedItems[0];

                for (int i = 0; i < listViewProfile.Items.Count; i++)
                {
                    if (listViewProfile.Items[i] != item)
                    {
                        listViewProfile.Items[i].BackColor = Color.FromArgb(32,32,32);
                    }
                    else
                    {
                        listViewProfile.Items[i].BackColor = Color.FromArgb(64, 64, 64);
                    }
                }


                Profile profile = ProfileManager.GetProfile((string)item.Tag);
                textBox_profilename.Text = profile.Config.ProfileName;
                textBox_csharpsourcefilename.Text = profile.Config.CSharpSourceFileName;
                textBox_targetprocess.Text = profile.Config.TargetProcess;
                comboBox_controller.Text = profile.Config.ControllerName;
                string[] supported_connames = profile.Config.ControllerName.Split(new string[1] { " && " }, StringSplitOptions.RemoveEmptyEntries);
                comboBox_controller.Items.Clear();
                comboBox_controller.Items.AddRange(supported_connames);
                for (int i = 0; i < checkedListBox_controllerlist.Items.Count; i++)
                {
                    if (supported_connames.Contains(checkedListBox_controllerlist.Items[i]))
                        checkedListBox_controllerlist.SetItemChecked(i, true);
                    else
                        checkedListBox_controllerlist.SetItemChecked(i, false);
                }
                richTextBox_description.Text = profile.Config.Description.Replace(@"\r\n", "\r\n").Replace(@"\n", "\n");
                richTextBoxCSharpCode.Text = profile.Config.CSharpSourceContent;
                IconPath = @"Profiles\" + profile.Config.ProfileID + @"\" + profile.Config.IconFileName;
                Config.IconFileName = profile.Config.IconFileName;
                // load image to memory without occupying the file.
                FileStream fs = new FileStream(IconPath, FileMode.Open, FileAccess.Read);
                int byteLength = (int)fs.Length;
                byte[] fileBytes = new byte[byteLength];
                fs.Read(fileBytes, 0, byteLength);
                fs.Close();
                pictureBox_profileicon.Image = Image.FromStream(new MemoryStream(fileBytes));
                SelectedProfile = profile;
            }
        }

        private void button_Stop_Click(object sender, EventArgs e)
        {
            for(int i=Running_Mapping.Count-1; i>=0; i--)
            {
                var map = Running_Mapping.ElementAt(i);
                var profile = map.Key;
                var cons = map.Value;
                if (profile.Config.ProfileID == SelectedProfile.Config.ProfileID)
                {
                    foreach (var con in cons)
                    {
                        Controller controller = Controllers.Get((int)con);
                        controller.Stop();
                        controller.UnMapToProfile(profile);
                        profile.Stop();
                    }
                    Running_Mapping.RemoveAt(i);
                }
            }
        }


        private void buttonApply_Click(object sender, EventArgs e)
        {
            if (Running_Mapping.Count == 0)
            {
                DumpValues();
                if (checkedListBox_controllerlist.CheckedIndices.Count > 0)
                {
                    Profile tmpprofile = new Profile(Config);
                    tmpprofile.Start();
                    if (tmpprofile.IsValid && tmpprofile.IsCompiled)
                    {
                        List<int> selected_cons = new List<int>();
                        foreach (int i in checkedListBox_controllerlist.CheckedIndices)
                            selected_cons.Add(i);
                        foreach (var sel_con in selected_cons)
                        {
                            Controller con = Controllers.Get((int)sel_con);
                            con.MapToProfile(tmpprofile);
                            con.Start();
                        }
                        Running_Mapping.Add(new KeyValuePair<Profile, List<int>>(tmpprofile, selected_cons));
                    }
                    else
                    {
                        Logger.Log("Profile is InValid or not compiled");
                    }
                }
                else
                    MessageBox.Show("No controllers selected!");
            }
        }



        private void buttonCreateNew_Click(object sender, EventArgs e)
        {
            SetDefaultText();
            DumpValues();
            ProfileManager.SaveProfile(isCreatingNew: true);
            LoadProfileToList();
        }

        private void buttonSaveAsNew_Click(object sender, EventArgs e)
        {
            DumpValues();
            ProfileManager.SaveProfile(isCreatingNew: true);
            LoadProfileToList();
        }

        private void pictureBox_profileicon_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "All Image Files|*.bmp;*.ico;*.gif;*.jpeg;*.jpg;*.png;*.tif;*.tiff|" +
                            "Windows Bitmap(*.bmp)|*.bmp|" +
                            "Windows Icon(*.ico)|*.ico|" +
                            "Graphics Interchange Format (*.gif)|(*.gif)|" +
                            "JPEG File Interchange Format (*.jpg)|*.jpg;*.jpeg|" +
                            "Portable Network Graphics (*.png)|*.png|" +
                            "Tag Image File Format (*.tif)|*.tif;*.tiff";
            openFileDialog1.RestoreDirectory = true;
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.FileName = "";
            openFileDialog1.Multiselect = false;
            openFileDialog1.Title = "Select an icon for your profile";

            if (DialogResult.OK == openFileDialog1.ShowDialog())
            {
                IconPath = openFileDialog1.FileName;
                Config.IconFileName = openFileDialog1.SafeFileName;
                FileStream fs = new FileStream(IconPath, FileMode.Open, FileAccess.Read);
                int byteLength = (int)fs.Length;
                byte[] fileBytes = new byte[byteLength];
                fs.Read(fileBytes, 0, byteLength);
                fs.Close();
                pictureBox_profileicon.Image = Image.FromStream(new MemoryStream(fileBytes));
            }
        }
        private void DumpValues()
        {
            Config.ProfileID = SelectedProfile.Config.ProfileID;
            Config.ProfileName = textBox_profilename.Text;
            Config.CSharpSourceFileName = textBox_csharpsourcefilename.Text;
            Config.TargetProcess = textBox_targetprocess.Text;
            Config.ControllerName = comboBox_controller.Text;
            Config.Description = richTextBox_description.Text;
            Config.CSharpSourceContent = richTextBoxCSharpCode.Text;
            // if selected icon from picture box, values will be updated in that event.
            if (IconPath == null)
            {
                pictureBox_profileicon_Click(this,null);
            }
        }

        private void SetDefaultText()
        {
            textBox_profilename.Text = "Template Profile";
            textBox_csharpsourcefilename.Text = "game_mapping.cs";
            textBox_targetprocess.Text = "explorer.exe";
            comboBox_controller.Text = "Controller(XBOX 360 For Windows)";
            richTextBox_description.Text = @"A template profile which you may insert your mapping into, using C#";
            richTextBoxCSharpCode.Text = @"using System;
using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Threading;
using ControllerGamer.Libraries;
using ControllerGamer.Libraries.Windows;
using ControllerGamer.Libraries.SimInput;
using ControllerGamer.Libraries.Controllers;

using Key = SharpDX.DirectInput.Key;
using EventType = SharpDX.DirectInput.JoystickOffset;

namespace GameProfile
{
    public class ControllerCallbacks
    {
        // Constructor
        public ControllerCallbacks()
        {
        }

        public void OnStickEventReceived(StickEventArgs stick_event)
        {
        }

        public void OnDPadEventReceived(DPadEventArgs dpad)
        {
        }

        public void OnButtonEventReceived(ButtonEventArgs button)
        {
        }

        public void OnMouseEventReceived(DMouseEventArgs e)
        {
        }

        public void OnKeyboardEventReceived(DKeyboardEventArgs e)
        {
        }

        public void OnMidiEventReceived(MidiEventArgs e)
        {
        }

        // show unhandled input
        public void OnEventReceived(ControllerEventArgs gamepad_event)
        {
            Logger.Log(gamepad_event.RawState);
        }
    }
}
";
        }

        private void CleanText()
        {
            textBox_profilename.Text = "";
            textBox_csharpsourcefilename.Text = "";
            richTextBox_description.Text = "";
            richTextBoxCSharpCode.Text = "";
            //richTextBox_log.Text = "";
            IconPath = null;
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            //ProfileManager.DeleteProfile(SelectedProfile.ProfileID);
            LoadControllerToList();
            LoadProfileToList();
        }
            


        Point mouseOff;
        bool leftFlag;

        private void CreateProfileDetail_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                mouseOff = new Point(-e.X, -e.Y);
                leftFlag = true;
            }
        }

        private void CreateProfileDetail_MouseMove(object sender, MouseEventArgs e)
        {
            if (leftFlag)
            {
                Point mouseSet = Control.MousePosition;
                mouseSet.Offset(mouseOff.X, mouseOff.Y);
                Location = mouseSet;
            }
        }

        private void CreateProfileDetail_MouseUp(object sender, MouseEventArgs e)
        {
            if (leftFlag)
            {
                leftFlag = false;
            }
        }

        private void button_Reload_Click(object sender, EventArgs e)
        {
            LoadControllerToList();
            LoadProfileToList();
        }

        private void richTextBox_log_TextChanged(object sender, EventArgs e)
        {
            richTextBox_log.SelectionStart = richTextBox_log.Text.Length;
            richTextBox_log.ScrollToCaret();
        }

        private void label_Exit_Click(object sender, EventArgs e)
        {
            System.Environment.Exit(0);
        }

        private bool Locked = true;
        private void button_lockunlock_Click(object sender, EventArgs e)
        {

            Locked = !Locked;

            buttonSaveCurrent.Visible = !Locked;
            buttonDelete.Visible = !Locked;

            richTextBoxCSharpCode.ReadOnly = Locked;
            textBox_profilename.ReadOnly = Locked;
            textBox_csharpsourcefilename.ReadOnly = Locked;
            richTextBox_description.ReadOnly = Locked;

            button_lockunlock.Text = Locked ? "Unlock" : "Lock";

        }

        private void button_listen_Click(object sender, EventArgs e)
        {
            if (button_listen.Text == "Listen")
            {
                NetListener.Start(int.Parse(textBox_port.Text));
                button_listen.Text = "Unlisten";
            }
            else
            {
                NetListener.Stop();
                button_listen.Text = "Listen";
            }
        }


        private void checkedListBox_controllerlist_SelectedValueChanged(object sender, EventArgs e)
        {
            string constr = "";
            foreach (var con in checkedListBox_controllerlist.CheckedItems)
            {
                constr += con;
                constr += " && ";
            }
            if(constr.Length>=4) comboBox_controller.Text = constr.Substring(0, constr.Length - 4);

        }
    }
}
