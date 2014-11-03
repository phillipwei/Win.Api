using System;
using NUnit.Framework;

namespace lib.test
{
    [TestFixture]
    public class WinApiTest
    {
        [Test]
        public void WinApi_ListWinData_Test()
        {
            var api = new WinApi();
            foreach(var win in api.ListWindowData(true))
            {
                Console.WriteLine(win);
            }
        }
    }
}
