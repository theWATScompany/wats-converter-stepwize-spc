using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml;
using Virinco.WATS.Interface;

namespace Virinco.WATS.Converter.StepWize
{
    public class GenericXSPCConverter : IReportConverter_v2
    {
        Dictionary<string, string> arguments;
        public Dictionary<string, string> ConverterParameters => arguments;

        public void CleanUp()
        {

        }

        public GenericXSPCConverter()
        {
            arguments = new Dictionary<string, string>() 
            {
                { "operator", "oper" },
                { "sequenceVersion", "1.0.0" },
                { "operationTypeCode", "10"}
            };
        }

        public GenericXSPCConverter(Dictionary<string, string> arguments)
        {
            this.arguments = arguments;
        }

        public Report ImportReport(TDM api, Stream file)
        {
            UUTReport uut = null;
            //api.TestMode = TestModeType.Import;
            using (XmlReader reader = XmlReader.Create(file))
            {
                XDocument xdoc = XDocument.Load(reader);
                uut = CreateUUTHeader(api, xdoc);
                UUTStatusType status = uut.Status;
                DateTime testEnd = AddMeasures(uut, xdoc);
                uut.ExecutionTime = (testEnd - uut.StartDateTime).TotalSeconds;
                uut.Status = status;
            }
            return uut;
        }

        private DateTime AddMeasures(UUTReport uut, XDocument xdoc)
        {
            //<steptype>0=Numeric,1=Action,2=PassFail
            SequenceCall sequence = uut.GetRootSequenceCall();
            Step currentStep = null;
            DateTime stepEnd = DateTime.MinValue;
            foreach (XElement step in xdoc.Element("ConfigAndResultsDataFile").Element("ConfigandResultsData").Element("Config_and_Result_Data").Elements("Config_and_Data_Result"))
            {
                if (step.Element("stepName").Value != sequence.Name)
                    sequence = uut.GetRootSequenceCall().AddSequenceCall(step.Element("stepName").Value);
                if (step.Element("dataType").Value == "0")
                {
                    double lowLimit = GetDbl(step, "lowerLimit");
                    double highLimit = GetDbl(step, "upperLimit");
                    double measure = GetDbl(step, "stepValue");
                    NumericLimitStep numericLimitStep = sequence.AddNumericLimitStep(step.Element("subItemName").Value);
                    CompOperatorType comp = GetCompOp(step);
                    if (comp.ToString().Length > 2)
                        numericLimitStep.AddTest(measure, comp, lowLimit, highLimit, step.Element("units").Value);
                    else if (comp.ToString().StartsWith("L"))
                        numericLimitStep.AddTest(measure, comp, highLimit, step.Element("units").Value);
                    else
                        numericLimitStep.AddTest(measure, comp, lowLimit, step.Element("units").Value);
                    currentStep = numericLimitStep;
                }
                else if (step.Element("dataType").Value == "1")
                {
                    PassFailStep passFailStep = sequence.AddPassFailStep(step.Element("subItemName").Value);
                    passFailStep.AddTest(step.Element("stepOutcome").Value == "Pass");
                    currentStep = passFailStep;
                }
                else if (step.Element("dataType").Value == "2")
                {
                    currentStep = sequence.AddGenericStep(GenericStepTypes.Action, step.Element("subItemName").Value);
                }
                else throw new ApplicationException($"Invalid dataType {step.Element("dataType").Value}");
                currentStep.Status = step.Element("stepOutcome").Value == "Pass" ? StepStatusType.Passed : StepStatusType.Failed;
                DateTime stepStart = DateTime.ParseExact(step.Element("startDateTime").Value, "yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
                stepEnd = DateTime.ParseExact(step.Element("endDateTime").Value, "yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
                currentStep.StepTime = (stepEnd - stepStart).TotalSeconds;
            }
            return stepEnd;
        }

        private double GetDbl(XElement step, string element)
        {
            double dbl = double.NaN;
            double.TryParse(step.Element(element).Value, NumberStyles.Any, CultureInfo.InvariantCulture, out dbl);
            return dbl;
        }

        CompOperatorType GetCompOp(XElement step)
        {
            string op = step.Element("limitMode").Value;
            switch (op)
            {
                case "Value > LL":
                case "Value &gt; LL": //return CompOperatorType.GT; Seem to be GE
                case "Value >= LL":
                case "Value &gt;= LL": return CompOperatorType.GE;
                case "LL &lt;= Value &lt;= UL":
                case "LL <= Value <= UL": return CompOperatorType.GELE;
                case "Value <= UL":
                case "Value &lt;= UL": return CompOperatorType.LE;
                case "Value < UL":
                case "Value &lt; UL": return CompOperatorType.LT;
                default: throw new ApplicationException($"Unhandled compare operator {step.Element("limitMode").Value}");
            }

        }


        private UUTReport CreateUUTHeader(TDM api, XDocument xdoc)
        {
            XElement header = xdoc.Element("ConfigAndResultsDataFile").Element("ConfigandResultsData");
            OperationType operationType = api.GetOperationTypes().Where(o => o.Name.ToLower() == header.Element("operation").Value.ToLower()).FirstOrDefault();
            if (operationType == null)
            {
                operationType = api.GetOperationType(ConverterParameters["operationTypeCode"]);
            }
            UUTReport uut = api.CreateUUTReport(
                arguments["operator"], header.Element("partNumber").Value, header.Element("revision").Value, header.Element("serialNumber").Value, operationType, header.Element("sourceFile").Value, arguments["sequenceVersion"]);
            XElement firstStep = header.Element("Config_and_Result_Data").Elements("Config_and_Data_Result").First();
            uut.StartDateTime = DateTime.ParseExact(firstStep.Element("startDateTime").Value, "yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
            if (firstStep.Element("assetID") != null)
                uut.StationName = firstStep.Element("assetID").Value;
            if (firstStep.Element("operatorStamp") != null)
                uut.Operator = firstStep.Element("operatorStamp").Value;
            AddMiscInfo(uut, header, "Organization");
            AddMiscInfo(uut, header, "resultSetID");
            AddMiscInfo(uut, header, "modelNumber");
            AddMiscInfo(uut, header, "partDescription");
            AddMiscInfo(uut, header, "label");
            AddMiscInfo(uut, header, "description");
            AddMiscInfo(uut, header, "build");
            AddMiscInfo(uut, header, "softwarePN");
            AddMiscInfo(uut, header, "workOrder");
            AddMiscInfo(uut, header, "factoryNumber");
            AddMiscInfo(uut, header, "exportRestriction");
            AddMiscInfo(uut, header, "plantName");
            AddMiscInfo(uut, header, "alternatePartNumber");
            AddMiscInfo(uut, header, "alternatePartNumber");
            if (header.Element("sequencePF").Value == "Fail")
                uut.Status = UUTStatusType.Failed;
            else if (header.Element("sequencePF").Value == "Incomplete")
                uut.Status = UUTStatusType.Terminated;
            return uut;
        }

        private void AddMiscInfo(UUTReport uut, XElement header, string element)
        {
            if (header.Element(element) == null)
                return;
            string value = header.Element(element).Value;
            if (!string.IsNullOrEmpty(value))
            {
                uut.AddMiscUUTInfo(element, value);
            }
        }
    }
}
