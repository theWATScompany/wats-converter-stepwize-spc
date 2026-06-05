using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virinco.WATS.Interface;

namespace Virinco.WATS.Converter.StepWize
{
    [TestClass]
    public class ConverterTest : TDM
    {
        [TestMethod]
        public void SetupClient()
        {
            SetupAPI(null, "location", "purpose", true);
            RegisterClient("Your WATS instance url", "username", "password/token");
            InitializeAPI(true);
        }

        [TestMethod]
        public void TestGenericXSPCConverter()
        {
            InitializeAPI(true);
            string fn = @"Data\20190516_133653_CONFIG_AND_RESULTS.XSPC";
            Dictionary<string, string> arguments = new GenericXSPCConverter().ConverterParameters;
            GenericXSPCConverter converter = new GenericXSPCConverter(arguments);
            using (FileStream file = new FileStream(fn, FileMode.Open))
            {
                SetConversionSource(new FileInfo(fn), converter.ConverterParameters, null);
                Report uut = converter.ImportReport(this, file);
                Submit(uut);
            }
        }

        [TestMethod]
        public void TestGenericXSPCConverterFolder()
        {
            InitializeAPI(true);
            Dictionary<string, string> arguments = new GenericXSPCConverter().ConverterParameters;
            GenericXSPCConverter converter = new GenericXSPCConverter(arguments);
            string[] fileNames = Directory.GetFiles(@"Data", "*.xspc", SearchOption.AllDirectories);
            bool startImport = true;
            foreach (string fn in fileNames)
            {
                using (FileStream file = new FileStream(fn, FileMode.Open))
                {
                    if (startImport)
                    {
                        SetConversionSource(new FileInfo(fn), converter.ConverterParameters, null);
                        Report uut = converter.ImportReport(this, file);
                        Submit(uut);
                    }
                }
            }
        }
    }
}
