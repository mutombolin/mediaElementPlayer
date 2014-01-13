using System.Collections.ObjectModel;
using PortableDeviceApiLib;

namespace PortableDevice
{
    public class PortableDeviceCollection : Collection<PortableDevice>
    {
        private readonly PortableDeviceManager _deviceManager;

        public PortableDeviceCollection()
        {
            this._deviceManager = new PortableDeviceManager();
        }

        public void Refresh()
        {
            this._deviceManager.RefreshDeviceList();

            // Determine how many WPD devices are connected
//            var deviceIds = new string[1];
            uint count = 1;
//            this._deviceManager.GetDevices(ref deviceIds[0], ref count);
            this._deviceManager.GetDevices(null, ref count);

            if (count <= 0)
                return;

            // Retrieve the device id for each connected device
//            deviceIds = new string[count];
//            this._deviceManager.GetDevices(ref deviceIds[0], ref count);
            string[] deviceIds = new string[count];
            this._deviceManager.GetDevices(deviceIds, ref count);

            foreach (var deviceId in deviceIds)
            {
                Add(new PortableDevice(deviceId));
            }
        }
    }
}