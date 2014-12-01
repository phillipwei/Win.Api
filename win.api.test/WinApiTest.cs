using System;
using NUnit.Framework;

namespace win.api.test
{
    [TestFixture]
    public class WinApiTest
    {
        [Test]
        public void WinApi_ListWinData_Test()
        {
            var api = new WinApi();
            foreach(var win in api.GetWindows(true))
            {
                Console.WriteLine(win);
            }
        }
    }
}
