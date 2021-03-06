﻿using ControllerGamer.Libraries.ProfileLoader;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ControllerGamer.Libraries.Controllers
{
    internal class Joystick :  SharpDX.DirectInput.Joystick , Controller
    {
        private Thread thread = null;
        private bool Running = false;
        
        private StickStatus LastStickStatus = new StickStatus();

        private event ControllerEventHandler EventReceived;

        public Joystick(Guid guid) : base(new DirectInput(),guid)
        {
            Properties.BufferSize = 1024;
        }
        

        private void _run()
        {
            List<int> helper_sticks = new List<int>() { 0, 4, 8, 12, 16, 20 };
            List<int> helper_pov = new List<int>() { 32,36,40,44 };

            while (Running == true)
            {
                Poll();
                var datas = GetBufferedData();
                foreach (var state in datas)
                {
                    if (EventReceived != null)
                    {
                        ControllerEventArgs cea = null;

                        if (helper_sticks.Contains(state.RawOffset)) cea = new StickEventArgs(state, LastStickStatus);
                        if (helper_pov.Contains(state.RawOffset)) cea = new DPadEventArgs(state);
                        if (state.RawOffset >=48 && state.RawOffset <=175) cea = new ButtonEventArgs(state);

                        if (cea==null) cea = new ControllerEventArgs(state);

                        EventReceived(cea);
                    }
                }
                Thread.Sleep(10);
            }
        }


        public bool Start()
        {
            if (thread == null)
            {
                Acquire();
                // restart. previous thread will automatically exit.
                Running = true;
                // start a new thread
                thread = new Thread(_run)
                {
                    IsBackground = true
                };
                thread.Start();
                return true;
            }
            return false ;
        }

        public bool Stop()
        {
            if (thread != null)
            {
                Running = false;
                Unacquire();
                return true;
            }
            else
                return false;
        }

        public void MapToProfile(Profile profile)
        {
            if (!profile.IsRunning)
                profile.Start();
            if (profile.IsRunning)
                EventReceived += profile.OnEventReceived;
        }
        public void UnMapToProfile(Profile profile)
        {
            if (!profile.IsRunning)
                profile.Start();
            if (profile.IsRunning)
                EventReceived -= profile.OnEventReceived;
        }
        public string GetDetail()
        {
            string res = "";
            res = res + "ControllerName: " + Information.ProductName + "\r\n";
            res = res + "Type: " + Information.Type + "\r\n";
            res = res + "ProductGuid: " + Information.ProductGuid + "\r\n";
            res = res + "PovCount: " + Capabilities.PovCount + "\r\n";
            res = res + "AxeCount: " + Capabilities.AxeCount + "\r\n";
            res = res + "ButtonCount: " + Capabilities.ButtonCount + "\r\n";
            res = res + "Flags: " + Capabilities.Flags + "\r\n";

            return res;
        }

        public string GetProductName()
        {
            return Information.ProductName;
        }
    }
}
