using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace win.api.test
{
    [TestClass]
    public class WinApiTest
    {
        [TestMethod]
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
