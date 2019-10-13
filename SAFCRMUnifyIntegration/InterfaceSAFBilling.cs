using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.Text;
using System.Xml;

namespace SAFCRMUnifyIntegration
{
   public class InterfaceSAFBilling : IPlugin
    {
        string CanNo = string.Empty;
        private string configData = string.Empty;
        private Dictionary<string, string> globalConfig = new Dictionary<string, string>();
        ITracingService tracingService;
        public InterfaceSAFBilling(string unsecureString, string secureString)
        {

            if (String.IsNullOrWhiteSpace(unsecureString) || String.IsNullOrWhiteSpace(secureString))
            {
                this.configData = unsecureString;
                this.ReadUnSecuredConfig(this.configData);
            }
            else
            {
                this.configData = unsecureString;
                this.ReadUnSecuredConfig(this.configData);
            }
        }
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {

                Entity workorders = (Entity)context.InputParameters["Target"];
                if (workorders.LogicalName != "onl_workorders")
                    return;
                Entity workorder = context.PostEntityImages["PostImage"];
                if (workorder.GetAttributeValue<OptionSetValue>("onl_spectra_orderstatus").Value == 122050005) //when status is Provisioning Completed
                {
                    EntityReference refsafid = workorder.GetAttributeValue<EntityReference>("spectra_safid");
                    Entity SAF = service.Retrieve("onl_saf", refsafid.Id, new ColumnSet(true));
                    //
                    if (SAF.Attributes.Contains("onl_orgcreationresponseonl"))
                    {
                        if (!SAF.GetAttributeValue<Boolean>("onl_orgcreationresponseonl"))
                            return;
                    }

                    CanNo = SAF.GetAttributeValue<string>("onl_spectra_accountid");

                    BillingAndSubscriptionRequest(service, SAF, CanNo, context,workorder);
                }
            }
        }
        public void BillingAndSubscriptionRequest(IOrganizationService service, Entity SAF,string canId, IPluginExecutionContext context,Entity workorder)
        {
            string SafNo = string.Empty;
            #region Request Values

           

            int childOrgId = 0, servicegroupno = 0;
            #region account details get
            QueryExpression query = new QueryExpression("account");
            query.NoLock = true;
            query.ColumnSet.AddColumns("alletech_accountno", "alletech_servicegroupno");
            query.Criteria.AddCondition("alletech_accountid", ConditionOperator.Equal, canId);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            EntityCollection resultcollection = service.RetrieveMultiple(query);

            if (resultcollection.Entities.Count > 0)
            {
                Entity account = resultcollection.Entities[0];
                if (account.Attributes.Contains("alletech_accountno"))
                    childOrgId = int.Parse(account.GetAttributeValue<string>("alletech_accountno"));
                else
                    throw new InvalidPluginExecutionException("Unify Account Id is empty for child account");

                if (account.Attributes.Contains("alletech_servicegroupno"))
                    servicegroupno = int.Parse(account.GetAttributeValue<string>("alletech_servicegroupno"));
            }
            else
            {
                throw new InvalidPluginExecutionException(" Account not found");
            }
          
            #endregion

            String advanceBilling = String.Empty;
            String billCycle = String.Empty;
            int billcycleno = 0;
            Boolean advance = true;
            Int32 billingFrequency = 0;
            String productId = String.Empty;
            int billProfileNo = 1, invoiceTemplateNo = 5, domSegment = 0;
            EntityReference seg = null;

            Entity safref = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_opportunityidid"));
            EntityReference owneropp = (EntityReference)safref.Attributes["onl_opportunityidid"];

            Entity Productrecord = service.Retrieve("opportunity", owneropp.Id, new ColumnSet("alletech_productsegment"));
            seg = Productrecord.GetAttributeValue<EntityReference>("alletech_productsegment");

            DateTime billStartDate2 = DateTime.Now.AddMonths(1);

            if (SAF.Attributes.Contains("onl_advanceonl"))
                advance = SAF.GetAttributeValue<bool>("onl_advanceonl");
            if (advance) advanceBilling = "true";
            else advanceBilling = "false";

            Entity Parent_SAfName = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_name")); //getting SAF Name on behalf of safid
            SafNo = Parent_SAfName.GetAttributeValue<String>("onl_name");//getting SAF Name

           

            #region First Invoice Date AND Bill End Date
            DateTime firstInvoiceDate = new DateTime();
            DateTime billEndDate = new DateTime();
            DateTime billInvoiceEndDate = new DateTime();
            #endregion
            #region Bill cycle information
            //string subscriptionStartDateString = DateFormater(DateTime.Now);//.AddHours(5).AddMinutes(30));

            string firstInvoiceDateString = null;
            string billEndDateString = null;
            Entity oppbillcycle = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_billcycleonl"));

            
           
            if (oppbillcycle.Attributes.Contains("onl_billcycleonl"))
            {
                Entity billCycleEnt = service.Retrieve("alletech_billcycle", oppbillcycle.GetAttributeValue<EntityReference>("onl_billcycleonl").Id, new ColumnSet("alletech_id", "alletech_days", "alletech_billcycleday"));
                billCycle = billCycleEnt.GetAttributeValue<String>("alletech_id").ToString();
                billcycleno = Convert.ToInt32(billCycle);

                
                if (seg.Name == "SDWAN")
                {
                    int billdays =  Convert.ToInt32(billCycleEnt.GetAttributeValue<String>("alletech_days"));

                    firstInvoiceDate = new DateTime(billStartDate2.Year, billStartDate2.Month, billdays);
                    billInvoiceEndDate = new DateTime(billStartDate2.Year, billStartDate2.Month, billdays + 1);
                    Entity prodseg = service.Retrieve("alletech_productsegment", seg.Id, new ColumnSet("spectra_invoicetemplate", "spectra_billprofile", "spectra_dunningprofile"));

                    billProfileNo = prodseg.GetAttributeValue<int>("spectra_billprofile");
                    invoiceTemplateNo = prodseg.GetAttributeValue<int>("spectra_invoicetemplate");
                    domSegment = prodseg.GetAttributeValue<int>("spectra_dunningprofile");
                   
                }
                else
                {
                    if (billCycleEnt.Attributes.Contains("alletech_billcycleday"))
                        firstInvoiceDate = new DateTime(billStartDate2.Year, billStartDate2.Month, billCycleEnt.GetAttributeValue<int>("alletech_billcycleday"));
                    else
                        throw new InvalidPluginExecutionException("Please add bill cycle day");
                }
                firstInvoiceDateString = DateFormater(firstInvoiceDate);

            }

            #endregion

            #region Based on Product get bill cycle details
            Entity oppproductonl = service.Retrieve("onl_workorders", workorder.Id, new ColumnSet("onl_productattached"));

           
            if (oppproductonl.Attributes.Contains("onl_productattached"))
            {
                Entity product = service.Retrieve("product", oppproductonl.GetAttributeValue<EntityReference>("onl_productattached").Id, new ColumnSet(true));//"alletech_billingcycle", "name"
               
                if (product.Attributes.Contains("alletech_billingcycle"))
                {
                    Entity billingCycle = service.Retrieve("alletech_billingcycle", product.GetAttributeValue<EntityReference>("alletech_billingcycle").Id, new ColumnSet("alletech_monthinbillingcycle"));
                    if (billingCycle.Attributes.Contains("alletech_monthinbillingcycle"))
                    {
                        billingFrequency = billingCycle.GetAttributeValue<Int32>("alletech_monthinbillingcycle");
                        billEndDate = firstInvoiceDate.AddMonths(billingFrequency);
                        billEndDateString = DateFormater(billEndDate);
                       
                        if(string.IsNullOrWhiteSpace(billEndDateString))
                        {
                            billEndDateString = DateFormater(billInvoiceEndDate);
                        }
                        if (product.Attributes.Contains("name"))
                        {
                            productId = oppproductonl.GetAttributeValue<EntityReference>("onl_productattached").Name;
                        }
                        else
                            throw new InvalidPluginExecutionException("Product does not have name!!");
                    }
                    else
                    {
                        throw new InvalidPluginExecutionException("Please Enter Bill frequency on Product.");
                    }
                }
                else
                {
                    billEndDateString = DateFormater(billInvoiceEndDate);
                    productId = oppproductonl.GetAttributeValue<EntityReference>("onl_productattached").Name;
                }
            }

            #endregion

            #endregion
            #region IP Address of Machine

            //IPHostEntry host;
            //string localIP = "?";

            //host = Dns.GetHostEntry(Dns.GetHostName());
            //foreach (IPAddress ip in host.AddressList)
            //{
            //    if (ip.AddressFamily == AddressFamily.InterNetwork)
            //    {
            //        localIP = ip.ToString();
            //        break;
            //    }
            //}
            #endregion

            //Entity oppID = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_opportunityidid"));
            //EntityReference ownerLookup = (EntityReference)oppID.Attributes["onl_opportunityidid"];
            //QueryExpression querycustomersite = new QueryExpression();
            //querycustomersite.EntityName = "onl_customersite";
            //querycustomersite.ColumnSet = new ColumnSet(true);
            //querycustomersite.Criteria.AddCondition("onl_opportunityidid", ConditionOperator.Equal, ownerLookup.Id);
            //EntityCollection resultcustomersite = service.RetrieveMultiple(querycustomersite);


            #region request XML
            String requestXml = String.Empty;
            requestXml = "<BillingSubscriptionRequest>" +
            "<CAF_No>" + SafNo + "</CAF_No>" +
            "<CAN_No>" + canId + "</CAN_No>" +
            "<BillRequest>" +
            "<actNo>" + servicegroupno + "</actNo>" +
            "<advanceBilling>" + advanceBilling + "</advanceBilling>" +
            "<billCycleNo>" + billcycleno + "</billCycleNo>" +
            "<billEndDate>" + billEndDateString + "</billEndDate>" +
            "<billProfileNo>" + billProfileNo + "</billProfileNo>" +
            "<billStartDate>" + firstInvoiceDateString + "</billStartDate>" +
            "<billCycle>" + billingFrequency + "</billCycle>" +
            "<billCycleDuration>M</billCycleDuration>" +
            "<firstInvoiceDate>" + firstInvoiceDateString + "</firstInvoiceDate>" +
            "<invoiceTemplateNo>" + invoiceTemplateNo + "</invoiceTemplateNo>" +
            "<receiptTemplateNo>3</receiptTemplateNo>";

            if (domSegment != 0)
                requestXml += "<domSegmentMapId>" + domSegment + "</domSegmentMapId>";

            requestXml += "</BillRequest>" +
            "<AddSubscription>" +
            "<alwaysOn>true</alwaysOn>" +
            "<createdDate>" + DateFormater(DateTime.Now) + "</createdDate>" +
            "<orgNo>" + childOrgId + "</orgNo>" +
            "<ratePlanID>" + productId + "</ratePlanID>" +
            "<serviceGroupNo>" + servicegroupno + "</serviceGroupNo>" +
            "<startDate>" + DateFormater(DateTime.Now) + "</startDate>" +
            "</AddSubscription>" +
            "<SessionObject>" +
            "<credentialId>1</credentialId>" +
            "<ipAddress>180.151.100.74</ipAddress>" +
            "<source>a</source>" +
            "<userName>crm.admin</userName>" +
            "<userType>123</userType>" +
            "<usrNo>10651</usrNo>" +
            "</SessionObject>" +
            "</BillingSubscriptionRequest>";

            #endregion
            if (context.Depth == 1)
            {
                if (requestXml.Contains("&"))
                {
                    requestXml = requestXml.Replace("&", "&amp;");
                }
                       
                #region Billing and Subcription request

                var uri = new Uri("http://jbossuat.spectranet.in:9002/rest/createBillSubscription/");


                Byte[] requestByte = Encoding.UTF8.GetBytes(requestXml);
                      
                //  throw new Exception("Request xml 222 ====== " + requestXml);

                WebRequest request = WebRequest.Create(uri);
                request.Method = WebRequestMethods.Http.Post;
                request.ContentLength = requestByte.Length;
                request.ContentType = "text/xml; encoding='utf-8'";
                request.GetRequestStream().Write(requestByte, 0, requestByte.Length);

                bool flag = false;

                using (var response = request.GetResponse())
                {
                    Entity IntegrationLog = new Entity("alletech_integrationlog_enterprise");
                    IntegrationLog["alletech_cafno"] = SafNo;
                    IntegrationLog["alletech_canno"] = canId;
                    IntegrationLog["alletech_billingrequest"] = requestXml;
                    IntegrationLog["alletech_name"] = "Subscription_Created_" + SafNo + "_" + canId;///Subscription_Created_
                    IntegrationLog["alletech_responsetype"] = new OptionSetValue(1);
                    Guid IntegrationLogId = service.Create(IntegrationLog);

                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(response.GetResponseStream());
                    string tmp = xmlDoc.InnerXml.ToString();

                    #region To create Integration Log from Response
                    XmlNodeList node1 = xmlDoc.GetElementsByTagName("BillingSubscriptionResponse");
                    for (int i = 0; i <= node1.Count - 1; i++)
                    {
                        string Code = node1[i].ChildNodes.Item(2).InnerText.Trim();
                        string Message = node1[i].ChildNodes.Item(3).InnerText.Trim();

                        Entity log = new Entity("alletech_integrationlog_enterprise");
                        log.Id = IntegrationLogId;
                        log["alletech_code"] = Code;
                        log["alletech_message"] = Message;
                        service.Update(log);
                        flag = true;
                    }
                    //if(flag==true)
                    //{
                    //    Entity _wko = new Entity("onl_workorders");
                    //    _wko.Id = workorder.Id;
                    //    _wko["spectra_billingrequest"] ="true";
                    //    service.Update(_wko);
                    //}
                    #endregion
                }
                #endregion
            }
        }
        public string DateFormater(DateTime d)
        {
            string day = d.Day.ToString();
            string month = d.Month.ToString();
            string year = d.Year.ToString();

            if (day.Length == 1)
                day = "0" + day;
            if (month.Length == 1)
                month = "0" + month;

            string date = year + "-" + month + "-" + day;
            string ISOformattedDate = date + "T00:00:00";//+05:30";
            return ISOformattedDate;

        }

        private void ReadUnSecuredConfig(string localConfig)
        {
            string key = string.Empty;
            try
            {
                this.globalConfig = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(localConfig))
                {
                    XmlDocument doc = new XmlDocument();

                    doc.LoadXml(localConfig);

                    foreach (XmlElement entityNode in doc.SelectNodes("/appSettings/add"))
                    {
                        key = entityNode.GetAttribute("key").ToString();
                        this.globalConfig.Add(entityNode.GetAttribute("key").ToString(), entityNode.GetAttribute("value").ToString());
                    }
                }
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                throw ex;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        private string GetValueForKey(string keyName)
        {
            string valueString = string.Empty;
            try
            {

                if (this.globalConfig.ContainsKey(keyName))
                {
                    valueString = this.globalConfig[keyName];
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return valueString;
        }
    }
}
