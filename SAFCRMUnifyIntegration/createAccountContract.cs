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
    public class createAccountContract : IPlugin
    {
        private static string config = string.Empty;
        private string configData = string.Empty;
        private Dictionary<string, string> globalConfig = new Dictionary<string, string>();
        ITracingService tracingService;
        string CanNo = string.Empty;
        public createAccountContract(string unsecureString, string secureString)
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
                    CanNo = SAF.GetAttributeValue<string>("onl_spectra_accountid");
                    CreateAccountContractRequest(service, SAF, CanNo, context, workorder);
                }

            }
        }
        public void CreateAccountContractRequest(IOrganizationService service, Entity SAF, String canId, IPluginExecutionContext context, Entity workorder)
        {
            #region variables and decalaration
            int servicegroupno = 0, subcrino = 0;
            string SafNo = string.Empty;
            String advanceBilling = String.Empty;
            String billCycle = String.Empty;
            int billcycleno = 0;
            DateTime billStartDate = new DateTime();
            string billstartDateString = null;
            Boolean advance = true;
            Int32 billingFrequency = 0;
            String productId = String.Empty;
            DateTime subscriptionStartDate = new DateTime();
            int billProfileNo = 1, invoiceTemplateNo = 5, domSegment = 0;
            decimal RC = 0;
            decimal NRC = 0;
            decimal IPAmount = 0;
            decimal PremiumInstallationCarge = 0;
            string ProdRatePlanId = string.Empty;
            string ipRatePlanId = string.Empty;
            //string seg = null;
            EntityReference seg = null;
            #endregion

            #region Account values
            QueryExpression query = new QueryExpression("account");
            query.NoLock = true;
            query.ColumnSet.AddColumns("alletech_subscriptionno", "alletech_servicegroupno");
            query.Criteria.AddCondition("alletech_accountid", ConditionOperator.Equal, canId);
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            EntityCollection acccollection = service.RetrieveMultiple(query);

            if (acccollection.Entities.Count > 0)
            {
                Entity account = acccollection.Entities[0];
                if (account.Attributes.Contains("alletech_subscriptionno"))
                    subcrino = account.GetAttributeValue<int>("alletech_subscriptionno");
                else
                    throw new InvalidPluginExecutionException("Unify subcrino is empty for child account");

                if (account.Attributes.Contains("alletech_servicegroupno"))
                    servicegroupno = int.Parse(account.GetAttributeValue<string>("alletech_servicegroupno"));

                tracingService.Trace("servicegroupno : " + servicegroupno);
                tracingService.Trace("subcrino : " + subcrino);
                subcrino = 2345;
            }
            else
            {
                throw new InvalidPluginExecutionException(" Account not found");
            }

            //throw new InvalidPluginExecutionException("account:-" + subcrino + "te:-" + servicegroupno);
            #endregion

            //#region IR
            //QueryExpression query = new QueryExpression("alletech_installationform");
            //query.NoLock = true;
            //query.ColumnSet.AddColumns("alletech_date", "alletech_date1");
            //query.Criteria.AddCondition("alletech_routetype", ConditionOperator.Equal, false);
            //query.Criteria.AddCondition("alletech_cafid", ConditionOperator.Equal, SAF.Id);
            //query.Criteria.AddCondition("spectra_canid", ConditionOperator.Equal, canId);
            //query.AddOrder("modifiedon", OrderType.Descending);
            //EntityCollection resultcollection = service.RetrieveMultiple(query);

            //Entity IR = resultcollection.Entities[0];
            //tracingService.Trace("IRCollection = " + resultcollection.Entities.Count);
            //#endregion


            Entity Parent_SAfName = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_name")); //getting SAF Name on behalf of safid
            SafNo = Parent_SAfName.GetAttributeValue<String>("onl_name");//getting SAF Name


            Entity safref = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_opportunityidid"));
            EntityReference owneropp = (EntityReference)safref.Attributes["onl_opportunityidid"];

            Entity Productrecord = service.Retrieve("opportunity", owneropp.Id, new ColumnSet("alletech_productsegment"));
            seg = Productrecord.GetAttributeValue<EntityReference>("alletech_productsegment");


            tracingService.Trace("prod seg: " + seg);

            DateTime billStartDate2 = DateTime.Now;

            if (SAF.Attributes.Contains("onl_advanceonl"))
                advance = SAF.GetAttributeValue<bool>("onl_advanceonl");
            if (advance) advanceBilling = "true";
            else advanceBilling = "false";



            #region First Invoice Date AND Bill End Date
            DateTime firstInvoiceDate = new DateTime();
            DateTime billEndDate = new DateTime();
            DateTime billInvoiceEndDate = new DateTime();

            #endregion
            #region bill cyle 
            string firstInvoiceDateString = null;
            string billEndDateString = null;
            string subscriptionStartDateString = null;

            //if (IR.GetAttributeValue<DateTime>("alletech_date") != null)
            //{
            //subscriptionStartDate = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(IR.Attributes["alletech_date"]), TimeZoneInfo.Local);
            //subscriptionStartDate = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(DateTime.Now), TimeZoneInfo.Local);
            subscriptionStartDateString = DateFormater(DateTime.Now);
            //}

            Entity oppbillcycle = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_billcycleonl"));

            if (oppbillcycle.Attributes.Contains("onl_billcycleonl"))
            {
                Entity billCycleEnt = service.Retrieve("alletech_billcycle", oppbillcycle.GetAttributeValue<EntityReference>("onl_billcycleonl").Id, new ColumnSet("alletech_id", "alletech_days", "alletech_billcycleday"));
                billCycle = billCycleEnt.GetAttributeValue<String>("alletech_id").ToString();
                billcycleno = Convert.ToInt32(billCycle);

                if (seg.Name == "SDWAN")
                {
                    // Convert.ToInt32(billCycleEnt.GetAttributeValue<String>("alletech_days"))
                    firstInvoiceDate = new DateTime(billStartDate2.Year, billStartDate2.Month, 3);
                    billInvoiceEndDate = new DateTime(billStartDate2.Year, billStartDate2.Month, 3 + 1);
                    firstInvoiceDateString = DateFormater(firstInvoiceDate);

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

                    if (firstInvoiceDate > DateTime.Now)
                        firstInvoiceDateString = DateFormater(firstInvoiceDate);
                    else
                    {
                        firstInvoiceDate = firstInvoiceDate.AddMonths(1);
                        firstInvoiceDateString = DateFormater(firstInvoiceDate);
                    }
                }
            }

            tracingService.Trace("firstInvoiceDateString : " + firstInvoiceDateString);

            //if (IR.Attributes.Contains("alletech_date1"))
            //{
            //billStartDate = IR.GetAttributeValue<DateTime>("alletech_date1");
            billStartDate = DateTime.Now;
            billstartDateString = DateFormater(billStartDate);
            //}
            tracingService.Trace("billstartDateString : " + billstartDateString);

            Entity oppproductonl = service.Retrieve("onl_workorders", workorder.Id, new ColumnSet("onl_productattached"));

            if (oppproductonl.Attributes.Contains("onl_productattached"))
            {
                Entity product = service.Retrieve("product", oppproductonl.GetAttributeValue<EntityReference>("onl_productattached").Id, new ColumnSet("alletech_billingcycle", "name"));
                if (product.Attributes.Contains("alletech_billingcycle"))
                {
                    Entity billingCycle = service.Retrieve("alletech_billingcycle", product.GetAttributeValue<EntityReference>("alletech_billingcycle").Id, new ColumnSet("alletech_monthinbillingcycle"));
                    if (billingCycle.Attributes.Contains("alletech_monthinbillingcycle"))
                    {
                        billingFrequency = billingCycle.GetAttributeValue<Int32>("alletech_monthinbillingcycle");
                        billEndDate = firstInvoiceDate.AddMonths(billingFrequency);
                        billEndDateString = DateFormater(billEndDate);
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

            // Check total no of Opportunity Sites 




            // getting Site based on opportunity id
            Entity opportunitEntity = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_opportunityidid"));
            EntityReference ownerLookup = (EntityReference)opportunitEntity.Attributes["onl_opportunityidid"];
            QueryExpression querycustomersite = new QueryExpression();
            querycustomersite.EntityName = "onl_customersite";
            querycustomersite.ColumnSet = new ColumnSet(true);
            querycustomersite.Criteria.AddCondition("onl_opportunityidid", ConditionOperator.Equal, ownerLookup.Id);
            EntityCollection resultcustomersite = service.RetrieveMultiple(querycustomersite);
            Entity Rev = resultcustomersite.Entities[0];

            #region RC and NRC Amount
            Entity opp = service.Retrieve("opportunity", owneropp.Id, new ColumnSet("alletech_customernameaccountlookup", "opportunityid"));


            string fetch =
            @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='opportunityproduct'>
                                <attribute name='productid' />
                                <attribute name='extendedamount' />
                                <attribute name='opportunityproductid' />
                                <order attribute='productid' descending='false' />
                                <filter type='and'>
                                  <condition attribute='onl_customerdetailsid' operator='eq' value='{" + Rev.Id + @"}' />
                                </filter>
                                <link-entity name='product' from='productid' to='productid' visible='false' link-type='outer' alias='oppprod'>
                                  <attribute name='alletech_plantype' />
                                  <attribute name='alletech_chargetype' />
                                </link-entity>
                              </entity>
                            </fetch>";

            EntityCollection oppProdCol = service.RetrieveMultiple(new FetchExpression(fetch));


            foreach (Entity oppprd in oppProdCol.Entities)
            {
                if (oppprd.Attributes.Contains("productid"))
                {
                    int plantype = ((OptionSetValue)oppprd.GetAttributeValue<AliasedValue>("oppprod.alletech_plantype").Value).Value;
                    int chargetype = ((OptionSetValue)oppprd.GetAttributeValue<AliasedValue>("oppprod.alletech_chargetype").Value).Value;


                    //normal and RC
                    if (plantype == 569480001 && chargetype == 569480001)
                    {
                        if (oppprd.Attributes.Contains("extendedamount"))
                            RC = oppprd.GetAttributeValue<Money>("extendedamount").Value;

                    }
                    else if (plantype == 569480001 && chargetype == 569480002)//nrml OTC
                    {
                        if (oppprd.Attributes.Contains("extendedamount"))
                            NRC = oppprd.GetAttributeValue<Money>("extendedamount").Value;
                    }
                    else if (plantype == 569480002 && chargetype == 569480001)//Addon IP
                    {
                        if (oppprd.Attributes.Contains("extendedamount"))
                        {
                            IPAmount = oppprd.GetAttributeValue<Money>("extendedamount").Value;
                            ipRatePlanId = ((Microsoft.Xrm.Sdk.EntityReference)(oppprd.Attributes["productid"])).Name.ToString();
                        }
                    }
                    else if (plantype == 569480002 && chargetype == 569480002)//addon PremiumInstallationCarge
                    {
                        if (oppprd.Attributes.Contains("extendedamount"))
                        {
                            PremiumInstallationCarge = oppprd.GetAttributeValue<Money>("extendedamount").Value;
                            ProdRatePlanId = ((Microsoft.Xrm.Sdk.EntityReference)(oppprd.Attributes["productid"])).Name.ToString();
                        }
                    }
                }
            }
            if(RC ==0 || NRC==0 )
            {
                if(IPAmount==0)
                {
                    throw new InvalidPluginExecutionException("Site Product is Empty");
                }
            }

            //if (opp.Attributes.Contains("alletech_customernameaccountlookup"))
            //{
            //    if (RC != 0)
            //        RC = RC / 2;

            //    if (NRC != 0)
            //        NRC = NRC / 2;

            //    if (IPAmount != 0)
            //        IPAmount = IPAmount / 2;

            //    if (PremiumInstallationCarge != 0)
            //        PremiumInstallationCarge = PremiumInstallationCarge / 2;
            //}
            #endregion

            #region request XML
            String requestXml = String.Empty;
            requestXml = "<CreateAccountContractRequest>" +
            "<CAF_No>" + SafNo + "</CAF_No>" +
            "<CAN_No>" + canId + "</CAN_No>" +
            "<CreateAccountContractDetails>" +
            "<subsNo>" + subcrino + "</subsNo>" +
            "<ratePlanID>" + productId + "</ratePlanID>" +
            "<servicegroupno>" + servicegroupno + "</servicegroupno>" +
            "<startDate>" + subscriptionStartDateString + "</startDate>" +
            "<RCAmount>" + RC + "</RCAmount>" +
            "<NRCAmount>" + NRC + "</NRCAmount>" +
            "<ipRcAmount>" + IPAmount + "</ipRcAmount>" +
            "<addtnlProdNrcAmount>" + PremiumInstallationCarge + "</addtnlProdNrcAmount>" +
            "<ipRatePlanId>" + ipRatePlanId + "</ipRatePlanId>" +
            "<addtnlProdRatePlanId>" + ProdRatePlanId + "</addtnlProdRatePlanId>" +
            "</CreateAccountContractDetails>" +
            "<BillRequest>" +
            "<actNo>" + servicegroupno + "</actNo>" +
            "<advanceBilling>" + advanceBilling + "</advanceBilling>" +
            "<billCycleNo>" + billcycleno + "</billCycleNo>" +
            "<billEndDate>" + billEndDateString + "</billEndDate>" +
            "<billProfileNo>" + billProfileNo + "</billProfileNo>" +
            "<billStartDate>" + firstInvoiceDateString + "</billStartDate>" +//installation date
            "<billCycle>" + billingFrequency + "</billCycle>" +
            "<billCycleDuration>M</billCycleDuration>" +
            "<firstInvoiceDate>" + firstInvoiceDateString + "</firstInvoiceDate>" +
            "<invoiceTemplateNo>" + invoiceTemplateNo + "</invoiceTemplateNo>" +
            "<receiptTemplateNo>3</receiptTemplateNo>";
            if (domSegment != 0)
                requestXml += "<domSegmentMapId>" + domSegment + "</domSegmentMapId>";
            requestXml += "</BillRequest>" +
            "<SessionObject>" +
            "<ipAddress>1.1.1.1< /ipAddress>" +
            "<userName>crm.admin</userName>" +
            "<usrNo>10651</usrNo>" +
            "</SessionObject>" +
            "</CreateAccountContractRequest>";

            #endregion

            // throw new InvalidPluginExecutionException("test:-" + requestXml);
            #region Billing and Subcription request
            var uri = new Uri("http://jbossuat.spectranet.in:9003/rest/createAccountContract/");

            Byte[] requestByte = Encoding.UTF8.GetBytes(requestXml);

            WebRequest request;
            try
            {
                request = WebRequest.Create(uri);
                request.Method = WebRequestMethods.Http.Post;
                request.ContentLength = requestByte.Length;
                request.ContentType = "text/xml; encoding='utf-8'";
                request.GetRequestStream().Write(requestByte, 0, requestByte.Length);

            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error in Request byte" + ex.StackTrace);
            }

            if (context.Depth == 1)
            {
                //Create Integration Log Enterprise
                using (var response = request.GetResponse())
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(response.GetResponseStream());
                    string tmp = xmlDoc.InnerXml.ToString();

                    #region To create Integration Log from Response
                    XmlNodeList node1 = xmlDoc.GetElementsByTagName("CreateAccountContractResponse");
                    for (int i = 0; i <= node1.Count - 1; i++)
                    {
                        string CAF_No = node1[i].ChildNodes.Item(0).InnerText.Trim();
                        string CAN_No = node1[i].ChildNodes.Item(1).InnerText.Trim();
                        //string Code = node1[i].ChildNodes.Item(2).InnerText.Trim();
                        string Message = node1[i].ChildNodes.Item(3).InnerText.Trim();


                        Entity IntegrationLog = new Entity("alletech_integrationlog_enterprise");
                        IntegrationLog["alletech_cafno"] = CAF_No;
                        IntegrationLog["alletech_canno"] = CAN_No;
                        IntegrationLog["alletech_accountcontractrequest"] = requestXml;
                        //IntegrationLog["alletech_code"] = Code;
                        IntegrationLog["alletech_message"] = Message;
                        IntegrationLog["alletech_name"] = "Contract_Created_" + CAF_No + "_" + CAN_No;//Contract_Created_
                        IntegrationLog["alletech_responsetype"] = new OptionSetValue(3);
                        service.Create(IntegrationLog);
                    }
                    #endregion

                    Entity SAFResponse = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_integration_responseonl", "onl_orgcreationresponseonl"));
                    //SAFResponse["onl_integration_responseonl"] = tmp;
                    SAFResponse["onl_orgcreationresponseonl"] = true;
                    service.Update(SAFResponse);
                }
            }

            #endregion
            //}
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
    }
}
