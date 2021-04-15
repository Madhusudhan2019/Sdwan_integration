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
           // throw new InvalidPluginExecutionException("BILLING depth: " + context.Depth);
            if (context.Depth == 1)
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
                {

                    Entity workorder = (Entity)context.InputParameters["Target"];
                    if (workorder.LogicalName != "onl_workorders")
                        return;
                    Entity workorders = context.PostEntityImages["PostImage"];
                    if (workorders.GetAttributeValue<bool>("spectra_serviceactivationflag") == true) 
                    {
                        EntityReference refsafid = workorders.GetAttributeValue<EntityReference>("spectra_safid");

                        //throw new InvalidPluginExecutionException("test");
                        Entity SAF = service.Retrieve("onl_saf", refsafid.Id, new ColumnSet(true));
                        tracingService.Trace("saf: " + SAF);// 
                        EntityReference refsite = workorders.GetAttributeValue<EntityReference>("onl_sitenameid");
                        Entity Sites = service.Retrieve("onl_customersite", refsite.Id, new ColumnSet("spectra_contractresponse", "spectra_siteaccountno"));
                        if (Sites.Attributes.Contains("spectra_contractresponse"))
                        {
                            if (Sites.GetAttributeValue<string>("spectra_contractresponse") == "Done")
                                return;
                        }
                        EntityReference ownerLookup = (EntityReference)SAF.Attributes["onl_opportunityidid"];

                        var opportunityName = ownerLookup.Name;
                        tracingService.Trace("opportunity NAme : " + opportunityName);
                        Guid opportunityid = ownerLookup.Id;
                        tracingService.Trace("opportunity name : " + opportunityid);
                        Entity Parent_accountid = service.Retrieve("opportunity", opportunityid, new ColumnSet("alletech_accountid"));

                        CanNo = Sites.GetAttributeValue<string>("spectra_siteaccountno");
                        tracingService.Trace("CAN No : " + CanNo);
                        BillingAndSubscriptionRequest(service, SAF, CanNo, context, workorders, opportunityid, Sites);
                        //}
                    }
                }
            }
        }
        public void BillingAndSubscriptionRequest(IOrganizationService service, Entity SAF,string canId, IPluginExecutionContext context,Entity workorder,Guid opportunityid,Entity Sites)
        {
            string SafNo = string.Empty;
             #region Request Values

           

            int childOrgId = 0, servicegroupno = 0;
            #region account details get

            #region update and get billcyle
            DateTime customerAcceptdate = workorder.GetAttributeValue<DateTime>("spectra_acceptedbycustomerdate");
            tracingService.Trace("aceptance Date: " + customerAcceptdate);
            int days = customerAcceptdate.Day;
            tracingService.Trace("days: " + days);
            EntityReference BusinessSegment = SAF.GetAttributeValue<EntityReference>("onl_businesssegmentonl");

            Entity siteproduct = service.Retrieve("onl_workorders", workorder.Id, new ColumnSet("onl_productattached"));
            Entity getproduct = service.Retrieve("product", siteproduct.GetAttributeValue<EntityReference>("onl_productattached").Id, new ColumnSet(true));

            EntityReference billfrequency = getproduct.GetAttributeValue<EntityReference>("alletech_billingcycle");
            tracingService.Trace("Bill frequency ID: " + billfrequency.Id);//
            tracingService.Trace("Bill frequency Name: " + billfrequency.Name);//

            string fetch = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
            <entity name='alletech_billcycle'>
            <attribute name='alletech_name' />
            <filter type='and'>
                <condition attribute='alletech_billfrequency' operator='eq' value='{" + billfrequency.Id + @"}' />
                <filter>
                <condition attribute='spectra_businesssegment' operator='eq' value='{" + BusinessSegment.Id + @"}' />
                <condition attribute='alletech_days' operator='like' value='%" + days + @"%' />
                </filter>
            </filter>
            </entity>
            </fetch>";
            tracingService.Trace("Fetch XML: " + fetch);
            EntityCollection resultbillcycle = service.RetrieveMultiple(new FetchExpression(fetch));
            tracingService.Trace("Bill cycle Count: " + resultbillcycle.Entities.Count);//
            if (resultbillcycle.Entities.Count > 0)
            {
                Entity alltech_billcyle = resultbillcycle.Entities[0];
                Guid billcyle = alltech_billcyle.GetAttributeValue<Guid>("alletech_billcycleid");
                tracingService.Trace("bill cycle: " + billcyle);
                EntityReference siteentityrefrenece = workorder.GetAttributeValue<EntityReference>("onl_sitenameid");

                Entity _site = new Entity("onl_customersite");
                _site.Id = Sites.Id;
                tracingService.Trace("site id: " + _site.Id);
                _site["onl_billcycle"] = new EntityReference("alletech_billcycle", billcyle);
                service.Update(_site);
                tracingService.Trace("Site updated");
            }



            #endregion
            tracingService.Trace("Account iD: " + canId);

            QueryExpression query = new QueryExpression("account");
            query.NoLock = true;
            query.ColumnSet.AddColumns("alletech_accountno", "alletech_servicegroupno");
            query.Criteria.AddCondition("alletech_accountid", ConditionOperator.Equal, canId);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            EntityCollection resultcollection = service.RetrieveMultiple(query);
            tracingService.Trace("Account retrive sucessufully");
            if (resultcollection.Entities.Count > 0)
            {
                Entity account = resultcollection.Entities[0];
                if (account.Attributes.Contains("alletech_accountno"))
                {
                    childOrgId = int.Parse(account.GetAttributeValue<string>("alletech_accountno"));
                    tracingService.Trace("child iD: " + childOrgId);
                }
                else
                    throw new InvalidPluginExecutionException("Unify Account Id is empty for child account");
                if (account.Attributes.Contains("alletech_servicegroupno"))
                {
                    servicegroupno = int.Parse(account.GetAttributeValue<string>("alletech_servicegroupno"));
                    tracingService.Trace("service group no: " + servicegroupno);
                }
            }
            else
            {
                throw new InvalidPluginExecutionException(" Account not found");
            }

           
            #endregion

            String advanceBilling = String.Empty;
            String billCycle = String.Empty;
            int billcycleno = 0;
            
            Int32 billingFrequency = 0;
            String productId = String.Empty;
            //int billProfileNo = 237,  domSegment = 0;
            int billProfileNo = 242, domSegment = 0;
            EntityReference seg = null;

            Entity safref = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_opportunityidid"));
            EntityReference owneropp = (EntityReference)safref.Attributes["onl_opportunityidid"];

            Entity Productrecord = service.Retrieve("opportunity", owneropp.Id, new ColumnSet("alletech_productsegment"));
            seg = Productrecord.GetAttributeValue<EntityReference>("alletech_productsegment");
            tracingService.Trace("seg: " + seg);
            DateTime billStartDate2 = DateTime.Now.AddMonths(1);
            tracingService.Trace("billing start date: " + billStartDate2);
            if (SAF.Attributes.Contains("onl_billtypeonl"))
            {
                //Advance
                if (SAF.GetAttributeValue<OptionSetValue>("onl_billtypeonl").Value == 122050000)
                {
                    advanceBilling = "true";
                    tracingService.Trace("advance billing ");
                }
                else
                {
                    advanceBilling = "false";
                    tracingService.Trace("not advance billing: ");
                }
            }
                
            Entity Parent_SAfName = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_name")); //getting SAF Name on behalf of safid
            SafNo = Parent_SAfName.GetAttributeValue<String>("onl_name");//getting SAF Name
            tracingService.Trace("saf No: " + SafNo);


            #region First Invoice Date AND Bill End Date
            DateTime firstInvoiceDate = new DateTime();
            DateTime DueInvoiceDate = new DateTime();
            DateTime billEndDate = new DateTime();
            DateTime billInvoiceEndDate = new DateTime();
            #endregion
            #region Bill cycle information
            //string subscriptionStartDateString = DateFormater(DateTime.Now);//.AddHours(5).AddMinutes(30));

            string firstInvoiceDateString = null;
            string billEndDateString = null;
           
            Entity oppbillcycle = service.Retrieve("onl_customersite", Sites.Id, new ColumnSet("onl_billcycle"));

            tracingService.Trace("Billcycle Name:" + oppbillcycle.GetAttributeValue<EntityReference>("onl_billcycle").Name);
            tracingService.Trace("Billcycle Id:" + oppbillcycle.GetAttributeValue<EntityReference>("onl_billcycle").Id);

            if (oppbillcycle.Attributes.Contains("onl_billcycle"))
            {
                Entity billCycleEnt = service.Retrieve("alletech_billcycle", oppbillcycle.GetAttributeValue<EntityReference>("onl_billcycle").Id, new ColumnSet("alletech_id", "alletech_days", "alletech_billcycleday"));
                billCycle = billCycleEnt.GetAttributeValue<String>("alletech_id").ToString();
                billcycleno = Convert.ToInt32(billCycle);
                tracingService.Trace("bill cycle number: " + billcycleno);

                //if (seg.Name == "SDN")
                //{
                //    int billdays =  days;
                    
                    
                //    firstInvoiceDate = new DateTime(billStartDate2.Year, billStartDate2.Month, billCycleEnt.GetAttributeValue<int>("alletech_billcycleday"));
                    
                //    tracingService.Trace("invoice date: " + firstInvoiceDate);
                //}
                //else
                //{
                    if (billCycleEnt.Attributes.Contains("alletech_billcycleday"))
                    {
                        firstInvoiceDate = new DateTime(billStartDate2.Year, billStartDate2.Month, billCycleEnt.GetAttributeValue<int>("alletech_billcycleday"));
                        tracingService.Trace("date invoice: " + firstInvoiceDate);
                    }
                    else
                        throw new InvalidPluginExecutionException("Please add bill cycle day");
                //}
                firstInvoiceDateString = DateFormater(firstInvoiceDate);
                tracingService.Trace("date string: " + firstInvoiceDateString);
               // DueInvoiceDate = new DateTime(billStartDate2.Year, billStartDate2.Month, billCycleEnt.GetAttributeValue<int>("alletech_billcycleday")+15);
            }

            #endregion

            #region Based on Product get bill cycle details 
            Entity oppproductonl = service.Retrieve("onl_workorders", workorder.Id, new ColumnSet("onl_productattached"));
            tracingService.Trace("product: " + oppproductonl);

            if (oppproductonl.Attributes.Contains("onl_productattached"))
            {
                Entity product = service.Retrieve("product", oppproductonl.GetAttributeValue<EntityReference>("onl_productattached").Id, new ColumnSet(true));//"alletech_billingcycle", "name"
               
                if (product.Attributes.Contains("alletech_billingcycle"))
                {
                    Entity billingCycle = service.Retrieve("alletech_billingcycle", product.GetAttributeValue<EntityReference>("alletech_billingcycle").Id, new ColumnSet("alletech_monthinbillingcycle"));
                    if (billingCycle.Attributes.Contains("alletech_monthinbillingcycle"))
                    {
                        billingFrequency = billingCycle.GetAttributeValue<Int32>("alletech_monthinbillingcycle");
                        tracingService.Trace("bill frquenncy: " + billingFrequency);
                        billEndDate = firstInvoiceDate.AddMonths(billingFrequency);
                        tracingService.Trace("end date of billing : " + billEndDate);
                        billEndDateString = DateFormater(billEndDate);
                        tracingService.Trace("end billing date string : " + billEndDateString);
                        if (string.IsNullOrWhiteSpace(billEndDateString))
                        {
                            billEndDateString = DateFormater(billInvoiceEndDate);
                            tracingService.Trace("end date of billing string: " + billEndDateString);
                        }
                        if (product.Attributes.Contains("name"))
                        {
                            productId = oppproductonl.GetAttributeValue<EntityReference>("onl_productattached").Name;
                            tracingService.Trace("product id: " + productId);
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
                    tracingService.Trace("product id: " + productId);
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
            "<invoiceTemplateNo>205</invoiceTemplateNo>" +
            "<receiptTemplateNo>3</receiptTemplateNo>";
            tracingService.Trace("request XML: " + requestXml);
            domSegment = 0;
            if (domSegment != 0)
            {
                requestXml += "<domSegmentMapId>" + domSegment + "</domSegmentMapId>";
                tracingService.Trace("request XML: " + requestXml);
            }
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
            tracingService.Trace("request XML: " + requestXml);

            #endregion
            //if (context.Depth == 1)
            //{
                if (requestXml.Contains("&"))
                {
                    requestXml = requestXml.Replace("&", "&amp;");
                    tracingService.Trace("request XML: " + requestXml);
                }
                       
                #region Billing and Subcription request

                var uri = new Uri("http://jbossprd.spectranet.in:9002/rest/createBillSubscription/");

                tracingService.Trace("URI: " + uri);

                Byte[] requestByte = Encoding.UTF8.GetBytes(requestXml);

                tracingService.Trace("request byte: " + requestByte);
                //  throw new Exception("Request xml 222 ====== " + requestXml);

                WebRequest request = WebRequest.Create(uri);
                request.Method = WebRequestMethods.Http.Post;
                request.ContentLength = requestByte.Length;
                request.ContentType = "text/xml; encoding='utf-8'";
                request.GetRequestStream().Write(requestByte, 0, requestByte.Length);

                bool flag = false;

                using (var response = request.GetResponse())
                {

                tracingService.Trace("Create Integration log enterprise: " );
                Entity IntegrationLog = new Entity("alletech_integrationlog_enterprise");
                    IntegrationLog["alletech_cafno"] = SafNo;
                    IntegrationLog["alletech_canno"] = canId;
                    IntegrationLog["alletech_billingrequest"] = requestXml;
                    IntegrationLog["alletech_name"] = "Subscription_Created_" + SafNo + "_" + canId;///Subscription_Created_
                    IntegrationLog["alletech_responsetype"] = new OptionSetValue(1);
                    Guid IntegrationLogId = service.Create(IntegrationLog);

                    tracingService.Trace("Integration record created");
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(response.GetResponseStream());
                    string tmp = xmlDoc.InnerXml.ToString();

                    tracingService.Trace("temp variable: " + tmp);
                    #region To create Integration Log from Response
                    XmlNodeList node1 = xmlDoc.GetElementsByTagName("BillingSubscriptionResponse");

                    tracingService.Trace("node: " + node1);
                    for (int i = 0; i <= node1.Count - 1; i++)
                    {
                        string Code = node1[i].ChildNodes.Item(2).InnerText.Trim();
                        tracingService.Trace("code: " + Code);
                        string Message = node1[i].ChildNodes.Item(3).InnerText.Trim();
                        tracingService.Trace("message: " + Message);

                        Entity log = new Entity("alletech_integrationlog_enterprise");
                        log.Id = IntegrationLogId;
                        log["alletech_code"] = Code;
                        log["alletech_message"] = Message;
                        EntityReference refsite = workorder.GetAttributeValue<EntityReference>("onl_sitenameid");
                        log["spectra_siteidid"] = new EntityReference("onl_customersite", refsite.Id);
                        Guid safid = SAF.Id;
                        log["onl_safid"] = new EntityReference("onl_saf", safid);
                        service.Update(log);
                        flag = true;

                        tracingService.Trace("integration log is updated");
                    }
                    if (flag == true)
                    {
                        EntityReference refsite = workorder.GetAttributeValue<EntityReference>("onl_sitenameid");
                        //spectra_contractresponse
                        tracingService.Trace("site id:"+refsite.Id);
                        Entity USites = service.Retrieve("onl_customersite", refsite.Id, new ColumnSet("spectra_contractresponse", "onl_billgenerationdate", "onl_billduedate"));
                        USites["spectra_contractresponse"] = "Done";

                        USites["onl_billgenerationdate"] = firstInvoiceDate;

                        tracingService.Trace("Bill Generation Date: " + firstInvoiceDate);
                        tracingService.Trace("Add 15 days add in bill due date ");

                        USites["onl_billduedate"] = firstInvoiceDate.AddDays(15);
                        tracingService.Trace("Bill Due date After add 15 days: " + DateFormater(firstInvoiceDate.AddDays(15)));

                     
                        service.Update(USites);

                        tracingService.Trace("site is updated");
                        System.Threading.Thread.Sleep(10000);
                        tracingService.Trace(" Process Wait");
                        //  workorder
                        Entity _wko = service.Retrieve("onl_workorders", workorder.Id, new ColumnSet("spectra_billingresponse"));
                        _wko["spectra_billingresponse"] = "True";
                        service.Update(_wko);

                        tracingService.Trace("workorder updated");
                    }
                    #endregion
                //}
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
