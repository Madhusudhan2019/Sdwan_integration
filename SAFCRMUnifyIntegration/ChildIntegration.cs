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
    public class ChildIntegration : IPlugin
    {
        int industryname;
        ITracingService tracingService;
        private string configData = string.Empty;
        Guid Productsegmentid = new Guid();
        private Dictionary<string, string> globalConfig = new Dictionary<string, string>();
        public ChildIntegration(string unsecureString, string secureString)
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

        public void Execute(IServiceProvider serviceProvider)
        {

            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
            
                
                Entity AccountID = (Entity)context.InputParameters["Target"];

                Entity integration = context.PostEntityImages["PostImage"];
                if (!integration.Contains("onl_safid"))
                {
                    return;
                    //throw new InvalidPluginExecutionException("caf");
                }
                else
                {
                    string SD_details = this.GetValueForKey("SdnSubBusiness");


                    //site subbusiness segment check condition
                    string accountid = integration.GetAttributeValue<string>("alletech_canno");

                    tracingService.Trace("Account id:" + accountid);

                    EntityReference refsafid = integration.GetAttributeValue<EntityReference>("onl_safid");
                    tracingService.Trace("Site id:" + refsafid.Id);
                    tracingService.Trace("Site Name:" + refsafid.Name);

                    Entity SAF = service.Retrieve("onl_saf", refsafid.Id, new ColumnSet(true));
                    Entity oppRef1 = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_opportunityidid"));
                    EntityReference ownerLookup = (EntityReference)oppRef1.Attributes["onl_opportunityidid"];

                    var opportunityName = ownerLookup.Name;
                    Guid opportunityid = ownerLookup.Id;
                    tracingService.Trace("Check Condition on Customer site based on opportunity id ");

                    QueryExpression querycustomersite = new QueryExpression();
                    querycustomersite.EntityName = "onl_customersite";
                    querycustomersite.ColumnSet = new ColumnSet("spectra_siteaccountno", "onl_sitetype", "onl_subbusinesssegment");
                    querycustomersite.Criteria.AddCondition("onl_opportunityidid", ConditionOperator.Equal, opportunityid);
                    EntityCollection resultcustomersite = service.RetrieveMultiple(querycustomersite);
                    Entity SiteEntity = resultcustomersite.Entities[0];
                    if (resultcustomersite.Entities.Count > 0)
                    {
                        string SubBusinessValue = SiteEntity.GetAttributeValue<EntityReference>("onl_subbusinesssegment").Name;
                        tracingService.Trace("Site Sub Business Value:-" + SubBusinessValue);
                        if (SubBusinessValue == SD_details)
                        {
                            string siteaccountid = SiteEntity.GetAttributeValue<string>("spectra_siteaccountno");
                            tracingService.Trace("Check Accountid match with site acoount id");
                            if (siteaccountid == accountid)//Acccount id and Site id match
                            {
                                tracingService.Trace("Check site type mode is Hub");
                                if (SiteEntity.GetAttributeValue<OptionSetValue>("onl_sitetype").Value == 122050000)//Hub  
                                {

                                    RequestFromChild(service, SAF, accountid, integration, context);
                                }
                            }
                            else
                            {
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }

            }
        }
        public void RequestFromChild(IOrganizationService service, Entity SAF, string CanNo, Entity integration, IPluginExecutionContext context)
        {

            #region SAF Variables
            string childName = string.Empty;
            string childshortname = string.Empty;
            string SafNo, Tan, Pan, Gst;
            // Payment
            string paymentAmount = string.Empty;
            decimal payamt = 0;
            string securityAmount = string.Empty;
            string paymentType = string.Empty;
            string paymentDate = string.Empty;
            DateTime paymentDateString = new DateTime();
            string chequeNo = string.Empty;
            int bankNo = 0;
            string banBranch = string.Empty;
            string securityDepositType = string.Empty;


            string creditcardno = string.Empty;
            string approvalcodecredit = string.Empty;
            string debitcardno = string.Empty;
            string approvalcodedebit = string.Empty;

            String firstnameAccShip = String.Empty;
            String lastnameAccShip = String.Empty;
            String billCityId = String.Empty;
            String ShipCityId = String.Empty;
            String firstName = String.Empty;
            String lastName = String.Empty;

            String shipsitecity = String.Empty;
            String billToStreet = String.Empty;
            String shipToStreet = String.Empty;
            String billToPincode = String.Empty;
            String shipToPincode = String.Empty;
            String billToState = String.Empty;
            String shipToState = String.Empty;
            String billToplotno = String.Empty;
            String billTofloor = String.Empty;
            String billTobuildingname = String.Empty;
            String billTolandmark = String.Empty;
            String billtostreetcomplete = String.Empty;
            String billtoarea = String.Empty;
            String shipToplotno = String.Empty;
            String shipTofloor = String.Empty;
            String shipTobuildingname = String.Empty;
            String shipTolandmark = String.Empty;
            String shiptostreetcomplete = String.Empty;
            String shiptoarea = String.Empty;
            String lastnameBill = String.Empty;
            string shipToblock = string.Empty;
            String billingContactfirst = String.Empty;


            String childFullName = String.Empty;
            string receiptLedgerAccountNo = string.Empty;
            string coaGroupNo = string.Empty;
            string coaNo = string.Empty;
            string ledgerBookNo = string.Empty;
            string ledgerParentId = string.Empty;
            string placeOfConsumptionId = string.Empty;
            String childDomainId = String.Empty;
            string CafInsCityRevGrpId = String.Empty;

            String LedgerparentId = String.Empty;
            #endregion
            #region Get Account Short name & account name
            tracingService.Trace("Get Account Detail based on Account id");
            ConditionExpression condition = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, CanNo);
            FilterExpression filter = new FilterExpression();
            filter.AddCondition(condition);
            filter.FilterOperator = LogicalOperator.And;
            QueryExpression query = new QueryExpression
            {
                EntityName = "account",
                ColumnSet = new ColumnSet(true),
                Criteria = filter,
            };
            EntityCollection parenntAccountCollection = service.RetrieveMultiple(query);
            Entity parentAccount = parenntAccountCollection.Entities[0];
            Guid AccountGuid = parentAccount.Id;
            tracingService.Trace("Get Parent Account count :" + parenntAccountCollection.Entities.Count);
            if (parentAccount.Attributes.Contains("name"))
                childName = parentAccount.GetAttributeValue<String>("name");
            tracingService.Trace("Get Parent Account Name :" + childName);

            if (parentAccount.Attributes.Contains("alletech_unifyshortname"))
                childshortname = parentAccount.GetAttributeValue<String>("alletech_unifyshortname");
            tracingService.Trace("Get Unify Account Short Name :" + childshortname);

            string paccshort = childshortname.Split('-')[0];



            #endregion
            #region SAF Details
            SafNo = SAF.GetAttributeValue<String>("onl_name");//getting SAF Name
            Tan = SAF.GetAttributeValue<String>("onl_tanno");//getting TAN Id on behalf of safid
            Pan = SAF.GetAttributeValue<String>("onl_panno");//getting PAN Id on behalf of safid
            Gst = SAF.GetAttributeValue<String>("onl_gstnumber");//getting PAN Id on behalf of safid

            tracingService.Trace("Get Saf Name :" + SafNo);
            tracingService.Trace("Get Saf Tan No :" + Tan);
            tracingService.Trace("Get Saf Pan No :" + Pan);
            tracingService.Trace("Get Saf GST No :" + Gst);

            Entity oppRef1 = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_opportunityidid"));
            EntityReference ownerLookup = (EntityReference)oppRef1.Attributes["onl_opportunityidid"];

            var opportunityName = ownerLookup.Name;
            Guid opportunityid = ownerLookup.Id; // getting opportunity id based on SAFID


            Entity oppsocalmedia = service.Retrieve("opportunity", opportunityid, new ColumnSet("alletech_facebookid", "alletech_twitterid", "alletech_emailid", "alletech_mobileno", "alletech_salutation"));
            var Facebook = oppsocalmedia.GetAttributeValue<String>("alletech_facebookid");
            var Twitter = oppsocalmedia.GetAttributeValue<String>("alletech_twitterid");
            var Email = oppsocalmedia.GetAttributeValue<String>("alletech_emailid");
            var Mobile = oppsocalmedia.GetAttributeValue<String>("alletech_mobileno");
            var salutation = oppsocalmedia.GetAttributeValue<OptionSetValue>("alletech_salutation").Value.ToString();

            tracingService.Trace("Get SAF Facebook :" + Facebook);
            tracingService.Trace("Get SAF Twitter :" + Twitter);
            tracingService.Trace("Get SAF Email :" + Email);
            tracingService.Trace("Get SAF Mobile :" + Mobile);
            tracingService.Trace("Generate Billing Record");
            if (SAF.Attributes.Contains("onl_cityonl"))
            {
                Entity city = service.Retrieve("alletech_city", SAF.GetAttributeValue<EntityReference>("onl_cityonl").Id, new ColumnSet("alletech_cityno"));
                billCityId = city.GetAttributeValue<String>("alletech_cityno");
                billCityId = string.IsNullOrWhiteSpace(billCityId) ? "0" : billCityId;
                tracingService.Trace("Billing City Id:-" + billCityId);
            }
            if (SAF.Attributes.Contains("onl_buildingnoplotnoonl"))
            {
                billToplotno = SAF.GetAttributeValue<string>("onl_buildingnoplotnoonl");
                billToplotno = billToplotno + ", ";
                tracingService.Trace("Billing plot:-" + billToplotno);
            }
            if (SAF.Attributes.Contains("onl_floor"))
            {

                billTofloor = SAF.GetAttributeValue<string>("onl_floor");
                billTofloor = billTofloor + ", ";
                billTofloor = "Floor - " + billTofloor;
                tracingService.Trace("Billing Floor:-" + billTofloor);
            }
            if (SAF.Attributes.Contains("onl_buildingnameonl"))
            {

                if (SAF.GetAttributeValue<EntityReference>("onl_buildingnameonl").Name.ToLower() == "other" && SAF.Attributes.Contains("onl_specifybuildingonl"))
                {
                    billTobuildingname = SAF.GetAttributeValue<string>("onl_specifybuildingonl");
                    billTobuildingname = billTobuildingname + ", ";
                }
                else if (SAF.GetAttributeValue<EntityReference>("onl_buildingnameonl").Name.ToLower() != "other")
                {
                    billTobuildingname = SAF.GetAttributeValue<EntityReference>("onl_buildingnameonl").Name;
                    billTobuildingname = billTobuildingname + ", ";
                }
                tracingService.Trace("Billing Building name:-" + billTobuildingname);
            }
            if (SAF.Attributes.Contains("onl_areaonl"))
            {
                if (SAF.GetAttributeValue<EntityReference>("onl_areaonl").Name.ToLower() == "other" && SAF.Attributes.Contains("onl_specifyareaonl"))
                {
                    billtoarea = SAF.GetAttributeValue<string>("onl_specifyareaonl");
                    billtoarea = billtoarea + ", ";
                }
                else if (SAF.GetAttributeValue<EntityReference>("onl_areaonl").Name.ToLower() != "other")
                {
                    billtoarea = SAF.GetAttributeValue<EntityReference>("onl_areaonl").Name;
                    billtoarea = billtoarea + ", ";
                }
                tracingService.Trace("Billing Area name:-" + billtoarea);
            }
            if (SAF.Attributes.Contains("onl_landmarkifany"))
            {
                billTolandmark = SAF.GetAttributeValue<string>("onl_landmarkifany");
                tracingService.Trace("Billing Landmark:-" + billTolandmark);
            }

            if (SAF.Attributes.Contains("onl_street"))
            {
                billToStreet = SAF.GetAttributeValue<String>("onl_street");
                billToStreet = billToStreet + ", ";
                tracingService.Trace("Billing Street:-" + billToStreet);
            }

            billtostreetcomplete = billToplotno + billTofloor + billTobuildingname + billToStreet + billtoarea + billTolandmark;
            tracingService.Trace("Billing Street concatenate :-" + billtostreetcomplete);

            if (SAF.Attributes.Contains("onl_pincode"))
            {
                billToPincode = SAF.GetAttributeValue<string>("onl_pincode").Replace(" ", "");
                tracingService.Trace("Billing Pin code :-" + billToPincode);
            }

            if (SAF.Attributes.Contains("onl_stateonl"))
            {
                Entity state = service.Retrieve("alletech_state", SAF.GetAttributeValue<EntityReference>("onl_stateonl").Id, new ColumnSet("alletech_statename"));
                billToState = state.GetAttributeValue<String>("alletech_statename");
                tracingService.Trace("Billing State :-" + billToState);
            }

            if (SAF.Attributes.Contains("onl_contactpersonname2"))
                billingContactfirst = SAF.GetAttributeValue<String>("onl_contactpersonname2");
            tracingService.Trace("Billing Contract Name  :-" + billingContactfirst);

            string[] ssize = billingContactfirst.Split(null);
            if (ssize.Length > 1)
            {
                firstnameAccShip = ssize[0];
                for (int k = 1; k < ssize.Length; k++)
                {
                    lastName = lastName + " " + ssize[k];
                }
            }
            else
                firstnameAccShip = ssize[0];

            Entity Productrecord = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_spectra_productsegment"));

            if (Productrecord.Attributes.Contains("onl_spectra_productsegment"))
            {
                string Productsegment = ((EntityReference)(Productrecord.Attributes["onl_spectra_productsegment"])).Name;
                Productsegmentid = ((EntityReference)(Productrecord.Attributes["onl_spectra_productsegment"])).Id;
                tracingService.Trace("Product Segment  :-" + Productsegment);
            }




            Entity oppty = service.Retrieve("opportunity", opportunityid, new ColumnSet("spectra_customersegmentcode"));
            if (oppty.Attributes.Contains("spectra_customersegmentcode"))
            {
                string customersegment = oppty.FormattedValues["spectra_customersegmentcode"].ToString();

                //if (customersegment.ToLower() == "sdwan")
                    industryname = 19;//smb
                //else if (customersegment.ToLower() == "la")
                //    industryname = 23;//LA
                //else if (customersegment.ToLower() == "media")
                //    industryname = 10;//media
                //else if (customersegment.ToLower() == "sp")
                //    industryname = 11;//service provider
            }
            else
            {
                industryname = 19;
                tracingService.Trace("Customer Segment is Empty on Lead");
            }
            #endregion
            #region payment mode type and bank details

            Entity cafPayment1 = service.Retrieve("onl_saf", SAF.Id, new ColumnSet("onl_payinslip"));


            EntityReference cafPayment = cafPayment1.GetAttributeValue<EntityReference>("onl_payinslip");


            Entity payment = service.Retrieve("alletech_payment", cafPayment.Id, new ColumnSet(true));


            if (payment.Attributes.Contains("alletech_paymentmodetype"))
            {
                // Payment Mode== CASH
                if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(10)))
                {

                    paymentType = "unify.finance.instrument.type.cash";

                    if (payment.Attributes.Contains("alletech_amount"))
                        payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;

                    if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                        securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                    if (payment.Attributes.Contains("alletech_securitydeposittype"))
                        securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                    if (payment.Attributes.Contains("alletech_paymentdate"))
                        paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                    paymentDate = DateFormater(paymentDateString);
                }
                //Payment Mode== CHEQUE
                if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(1)))
                {
                    paymentType = "unify.finance.instrument.type.cheque";
                    if (payment.Attributes.Contains("alletech_amount"))
                        payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                    if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                        securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                    if (payment.Attributes.Contains("alletech_securitydeposittype"))
                        securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                    if (payment.Attributes.Contains("alletech_chequeddno"))
                        chequeNo = payment.GetAttributeValue<string>("alletech_chequeddno");
                    if (payment.Attributes.Contains("alletech_chequeddissuedate"))
                        paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_chequeddissuedate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                    paymentDate = DateFormater(paymentDateString);

                    EntityReference bankname = payment.GetAttributeValue<EntityReference>("alletech_banknameid");
                    Entity _bank = service.Retrieve("alletech_bank", bankname.Id, new ColumnSet("alletech_banknameid"));
                    if (_bank.Attributes.Contains("alletech_banknameid"))
                    {
                        bankNo = _bank.GetAttributeValue<Int32>("alletech_banknameid");
                    }
                    if (payment.Attributes.Contains("alletech_branch"))
                        banBranch = payment.GetAttributeValue<string>("alletech_branch");

                }
                //Payment Mode== CREDIT CARD
                if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(7)))
                {
                    paymentType = "unify.finance.instrument.type.creditCard";
                    if (payment.Attributes.Contains("alletech_amount"))
                        payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                    if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                        securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                    if (payment.Attributes.Contains("alletech_securitydeposittype"))
                        securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                    if (payment.Attributes.Contains("alletech_refrenceid"))
                        approvalcodecredit = payment.GetAttributeValue<string>("alletech_refrenceid");
                    if (payment.Attributes.Contains("alletech_creditcardlast4digitno"))
                        creditcardno = payment.GetAttributeValue<string>("alletech_creditcardlast4digitno");

                    chequeNo = approvalcodecredit + " - " + creditcardno;

                    //if (payment.Attributes.Contains("alletech_paymentdate"))
                    if (payment.Attributes.Contains("alletech_paymentdate"))
                        paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                    paymentDate = DateFormater(paymentDateString);
                }


                //Payment Mode== DEBIT CARD
                if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(8)))
                {
                    paymentType = "unify.finance.instrument.type.debitCard";
                    if (payment.Attributes.Contains("alletech_amount"))
                        payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                    if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                        securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                    if (payment.Attributes.Contains("alletech_securitydeposittype"))
                        securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                    if (payment.Attributes.Contains("alletech_debitcardapprovalcode"))
                        approvalcodedebit = payment.GetAttributeValue<string>("alletech_debitcardapprovalcode");
                    if (payment.Attributes.Contains("alletech_debitcardlast4digitno"))
                        debitcardno = payment.GetAttributeValue<string>("alletech_debitcardlast4digitno");

                    chequeNo = approvalcodedebit + " - " + debitcardno;

                    //if (payment.Attributes.Contains("alletech_paymentdate"))
                    if (payment.Attributes.Contains("alletech_paymentdate"))
                        paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                    paymentDate = DateFormater(paymentDateString);
                }

                //Payment Mode== DD
                if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(2)))
                {
                    paymentType = "unify.finance.instrument.type.demanddraft";
                    if (payment.Attributes.Contains("alletech_amount"))
                        payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                    if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                        securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                    if (payment.Attributes.Contains("alletech_securitydeposittype"))
                        securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                    if (payment.Attributes.Contains("alletech_chequeddno"))
                        chequeNo = payment.GetAttributeValue<string>("alletech_chequeddno");
                    if (payment.Attributes.Contains("alletech_chequeddissuedate"))
                        paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_chequeddissuedate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                    paymentDate = DateFormater(paymentDateString);

                    EntityReference bankname = payment.GetAttributeValue<EntityReference>("alletech_banknameid");
                    Entity _bank = service.Retrieve("alletech_bank", bankname.Id, new ColumnSet("alletech_banknameid"));
                    if (_bank.Attributes.Contains("alletech_banknameid"))
                    {
                        bankNo = _bank.GetAttributeValue<Int32>("alletech_banknameid");

                    }
                    if (payment.Attributes.Contains("alletech_branch"))
                        banBranch = payment.GetAttributeValue<string>("alletech_branch");

                }

                //Payment Mode== RTGS
                if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(5)))
                {
                    paymentType = "unify.finance.instrument.type.rtgs";
                    if (payment.Attributes.Contains("alletech_amount"))
                        payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                    if (payment.Attributes.Contains("alletech_transactionreferenceid"))
                        chequeNo = payment.GetAttributeValue<string>("alletech_transactionreferenceid");
                    if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                        securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                    if (payment.Attributes.Contains("alletech_securitydeposittype"))
                        securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                    //if (payment.Attributes.Contains("alletech_paymentdate"))
                    if (payment.Attributes.Contains("alletech_paymentdate"))
                        paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                    paymentDate = DateFormater(paymentDateString);
                }

                //Payment Mode== NEFT
                if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(4)))
                {
                    paymentType = "unify.finance.instrument.type.neft";
                    if (payment.Attributes.Contains("alletech_amount"))
                        payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                    if (payment.Attributes.Contains("alletech_transactionreferenceid"))
                        chequeNo = payment.GetAttributeValue<string>("alletech_transactionreferenceid");
                    if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                        securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                    if (payment.Attributes.Contains("alletech_securitydeposittype"))
                        securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                    // if (payment.Attributes.Contains("alletech_paymentdate"))
                    if (payment.Attributes.Contains("alletech_paymentdate"))
                        paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                    paymentDate = DateFormater(paymentDateString);
                }

                //Paymentmode == EZETAP
                if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(11)))
                {
                    paymentType = "unify.finance.instrument.type.ezetap";
                    if (payment.Attributes.Contains("alletech_amount"))
                        payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                    if (payment.Attributes.Contains("alletech_transactionreferenceid"))
                        chequeNo = payment.GetAttributeValue<string>("alletech_transactionreferenceid");
                    if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                        securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                    if (payment.Attributes.Contains("alletech_securitydeposittype"))
                        securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                    // if (payment.Attributes.Contains("alletech_paymentdate"))
                    if (payment.Attributes.Contains("alletech_paymentdate"))
                        paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_paymentdate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                    paymentDate = DateFormater(paymentDateString);
                }

                //Paymentmode == EZETAP - CHEQUE
                if (payment.GetAttributeValue<OptionSetValue>("alletech_paymentmodetype").Equals(new OptionSetValue(12)))
                {
                    paymentType = "unify.finance.instrument.type.ezetapcheque";
                    if (payment.Attributes.Contains("alletech_amount"))
                        payamt = payment.GetAttributeValue<Money>("alletech_amount").Value;
                    if (payment.Attributes.Contains("alletech_securitydepositifapplicable"))
                        securityAmount = payment.GetAttributeValue<string>("alletech_securitydepositifapplicable");

                    if (payment.Attributes.Contains("alletech_securitydeposittype"))
                        securityDepositType = payment.GetAttributeValue<OptionSetValue>("alletech_securitydeposittype").Value.ToString();

                    if (payment.Attributes.Contains("alletech_chequeddno"))
                        chequeNo = payment.GetAttributeValue<string>("alletech_chequeddno");
                    if (payment.Attributes.Contains("alletech_chequeddissuedate"))
                        paymentDateString = TimeZoneInfo.ConvertTimeFromUtc(Convert.ToDateTime(payment.Attributes["alletech_chequeddissuedate"]), TimeZoneInfo.Local);//payment.GetAttributeValue<DateTime>("alletech_paymentdate").AddHours(5).AddMinutes(30).ToShortDateString();
                    paymentDate = DateFormater(paymentDateString);

                    EntityReference bankname = payment.GetAttributeValue<EntityReference>("alletech_banknameid");
                    Entity _bank = service.Retrieve("alletech_bank", bankname.Id, new ColumnSet("alletech_banknameid"));
                    if (_bank.Attributes.Contains("alletech_banknameid"))
                        bankNo = _bank.GetAttributeValue<Int32>("alletech_banknameid");

                    if (payment.Attributes.Contains("alletech_branch"))
                        banBranch = payment.GetAttributeValue<string>("alletech_branch");
                }
            }

            #endregion

            var requestXml = "";
            #region constructing Request xml
            QueryExpression querycustomer = new QueryExpression();
            querycustomer.EntityName = "onl_customersite";
            querycustomer.ColumnSet = new ColumnSet(true);
            querycustomer.Criteria.AddCondition("onl_opportunityidid", ConditionOperator.Equal, opportunityid);
            EntityCollection resultcustomer = service.RetrieveMultiple(querycustomer);
            decimal Sitecount = resultcustomer.Entities.Count;
            //Payment Amount Divided by Total no of Sites
            paymentAmount = (payamt / Sitecount).ToString();
            //Security Amount Divided by Total  no of Sites
            if (securityAmount != null && securityAmount != string.Empty)
                securityAmount = (decimal.Parse(securityAmount) / Sitecount).ToString();

            tracingService.Trace("Payment Amount  :-" + paymentAmount);
            tracingService.Trace("Security Amount  :-" + securityAmount);

            QueryExpression querycustomersiteBranch = new QueryExpression();
            querycustomersiteBranch.EntityName = "onl_customersite";
            querycustomersiteBranch.ColumnSet = new ColumnSet(true);
            querycustomersiteBranch.Criteria.AddCondition("onl_opportunityidid", ConditionOperator.Equal, opportunityid);
            querycustomersiteBranch.Criteria.AddCondition("onl_sitetype", ConditionOperator.Equal, "122050001");
            EntityCollection resultcustomersitebranch = service.RetrieveMultiple(querycustomersiteBranch);
            tracingService.Trace("Customer Site With out Hub Site type count  :-" + resultcustomersitebranch.Entities.Count);
            #region Integration log get unify account no
            string account_unifyparentno = integration.GetAttributeValue<string>("alletech_unify_parentorgid");
            #endregion

            bool Branchflag = false;
            int rowcount = 2;
            foreach (Entity Siteentity in resultcustomersitebranch.Entities)
            {
                string CreateSiteAccount = "CreateSiteAccount";
                //Generate Account Based on Site Id & Get Account Created Id
                CanNo = ChildcountSitesCreation(service, ref SAF, opportunityid, context, rowcount, childshortname, CreateSiteAccount, Siteentity);
                ConditionExpression conditionacc = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, CanNo);
                FilterExpression filteracc = new FilterExpression();
                filteracc.AddCondition(conditionacc);
                filteracc.FilterOperator = LogicalOperator.And;
                QueryExpression queryacc = new QueryExpression
                {
                    EntityName = "account",
                    ColumnSet = new ColumnSet(true),
                    Criteria = filter,
                };
                EntityCollection childAccountC = service.RetrieveMultiple(queryacc);
                if (childAccountC.Entities.Count == 0)
                    return;
                Entity childPAccount = childAccountC.Entities[0];
                Guid pacid = childPAccount.Id;

                #region getting site details

                //city id based on each site
                Guid sitecity = Siteentity.GetAttributeValue<EntityReference>("onl_city").Id;
                string sitecityname = Siteentity.GetAttributeValue<EntityReference>("onl_city").Name;
                if (Siteentity.Attributes.Contains("onl_city"))
                {
                    Entity Sitecity = service.Retrieve("alletech_city", Siteentity.GetAttributeValue<EntityReference>("onl_city").Id, new ColumnSet("alletech_cityno"));
                    shipsitecity = Sitecity.GetAttributeValue<String>("alletech_cityno");
                    shipsitecity = string.IsNullOrWhiteSpace(shipsitecity) ? "0" : shipsitecity;
                }
                if (Siteentity.Attributes.Contains("onl_state"))
                {
                    Entity state = service.Retrieve("alletech_state", Siteentity.GetAttributeValue<EntityReference>("onl_state").Id, new ColumnSet("alletech_statename"));
                    shipToState = state.GetAttributeValue<String>("alletech_statename");
                }
                if (Siteentity.Attributes.Contains("spectra_pincode"))
                    shipToPincode = Siteentity.GetAttributeValue<String>("spectra_pincode");
                string SiteAddress = Siteentity.GetAttributeValue<String>("onl_address");
                //City domain based on sitecity and product 
                QueryExpression querydom = new QueryExpression();
                querydom.EntityName = "alletech_domain";
                querydom.ColumnSet.AddColumns("alletech_name", "alletech_domain_id");
                querydom.Criteria.AddCondition("spectra_productsegment", ConditionOperator.Equal, Productsegmentid);
                querydom.Criteria.AddCondition("pcl_city", ConditionOperator.Equal, sitecity);
                EntityCollection result1 = service.RetrieveMultiple(querydom);
                Entity domainentity = result1.Entities[0];
                if (domainentity.Attributes.Contains("alletech_domain_id"))
                    childDomainId = domainentity.Attributes["alletech_domain_id"].ToString();

                #region Reevenue group Entity record
                QueryExpression queryConfig = new QueryExpression("alletech_revenuegroup");
                queryConfig.ColumnSet = new ColumnSet(true);
                queryConfig.Criteria.AddCondition("spectra_productsegment", ConditionOperator.Equal, Productsegmentid);
                queryConfig.Criteria.AddCondition("alletech_name", ConditionOperator.Equal, sitecityname);
                EntityCollection RevenuegroupCollection = service.RetrieveMultiple(queryConfig);

                Entity Rev = RevenuegroupCollection.Entities[0];
                if (RevenuegroupCollection.Entities.Count > 0)
                {
                    // Entity Rev = RevenuegroupCollection.Entities[0]; 
                    if (Rev.Attributes.Contains("alletech_id"))
                    {
                        CafInsCityRevGrpId = Rev.GetAttributeValue<string>("alletech_id");
                    }
                    if (Rev.Attributes.Contains("alletech_revenuecoagroupid"))
                    {
                        coaGroupNo = Rev.GetAttributeValue<int>("alletech_revenuecoagroupid").ToString();
                    }
                    if (Rev.Attributes.Contains("alletech_coano"))
                    {
                        coaNo = Rev.GetAttributeValue<int>("alletech_coano").ToString();
                    }
                    if (Rev.Attributes.Contains("alletech_ledgerbookno"))
                    {
                        ledgerBookNo = Rev.GetAttributeValue<int>("alletech_ledgerbookno").ToString();
                    }
                    if (Rev.Attributes.Contains("alletech_ledgerparentid"))
                    {
                        ledgerParentId = Rev.GetAttributeValue<int>("alletech_ledgerparentid").ToString();
                    }

                    if (Rev.Attributes.Contains("alletech_placeofconsumptionid"))
                    {
                        placeOfConsumptionId = Rev.GetAttributeValue<int>("alletech_placeofconsumptionid").ToString();

                    }
                    if (Rev.Attributes.Contains("alletech_receiptledgeraccountno"))
                    {
                        receiptLedgerAccountNo = Rev.GetAttributeValue<int>("alletech_receiptledgeraccountno").ToString();
                    }
                }
                #endregion

                var sitename = Siteentity.GetAttributeValue<String>("spectra_customername");
                string[] ssizefullnmae = sitename.Split(null);
                if (ssizefullnmae.Length > 1)
                {
                    firstName = ssizefullnmae[0];
                    for (int k = 1; k < ssizefullnmae.Length; k++)
                    {
                        lastnameAccShip = lastnameAccShip + " " + ssizefullnmae[k];
                    }
                }
                else
                    firstName = ssizefullnmae[0];

                #endregion

                #region start xml  
                requestXml = "";
                requestXml += "<CRMCAFRequest>" +
                "<CAF_No>" + SafNo + "</CAF_No>" +
                "<CAN_No>" + CanNo + "</CAN_No>" +
                "<ChildOrganisationRequest>" +
               "<customer>true</customer>" +
               "<domain>" + childDomainId + "</domain>" +
               "<name>" + childName + "</name>" +
               "<parentNo>" + account_unifyparentno + "</parentNo>" +
               "<shortName>" + paccshort + "-0" + rowcount + "</shortName>" +
               "<revenueGroupId>" + CafInsCityRevGrpId + "</revenueGroupId>" +
               "<receiptLedgerAccountNo>" + receiptLedgerAccountNo + "</receiptLedgerAccountNo>" +
               "<societyName>" + "NA" + "</societyName>" +
                "<areaName>" + "NA" + "</areaName>" +
                "<societyFieldId>2</societyFieldId>" +
                "<areaFieldId>3</areaFieldId>" +
                "<productSegmentNo>6</productSegmentNo>" +
                "<verticalSegmentNo>10</verticalSegmentNo>" +
                "<industryTypeNo>" + industryname + "</industryTypeNo>" +
               "</ChildOrganisationRequest>";

                #region Contact details

                #region Billing
                requestXml += "<ContactDetails>" +
                "<cityNo>" + billCityId + "</cityNo>" +
                "<contactTypeNo>1</contactTypeNo>" +
                "<firstName>" + firstnameAccShip + "</firstName>";
                if (!String.IsNullOrEmpty(lastName))
                    requestXml = requestXml + "<lastName>" + lastName + "</lastName>";
                requestXml += "<salutationNo>" + salutation + "</salutationNo>" +
                "<pin>" + billToPincode + "</pin>" +
                "<state>" + billToState + "</state>" +
                "<street>" + billtostreetcomplete + "</street>" +
                "</ContactDetails>" +
                #endregion

                #region  Shipping Address
                                "<ContactDetails>" +
               "<cityNo>" + shipsitecity + "</cityNo>" +
                "<contactTypeNo>2</contactTypeNo>" +
                "<firstName>" + firstName + "</firstName>";

                if (!String.IsNullOrEmpty(lastnameAccShip))
                    requestXml = requestXml + "<lastName>" + lastnameAccShip + "</lastName>";

                requestXml = requestXml +
                "<salutationNo>" + salutation + "</salutationNo>" +
                "<pin>" + shipToPincode + "</pin>" +
                "<state>" + shipToState + "</state>" +
                "<street>" + SiteAddress + "</street>" +
                "</ContactDetails>";
                #endregion

                #endregion


                #region Bill  to communication
                if (Email != null)
                {
                    requestXml = requestXml + "<commMode>" +
                    "<commTypeNo>4</commTypeNo>" +
                    //"<contactNo></contactNo>" +     //  ----- bill email
                    "<isDefault>false</isDefault>" +
                    "<dnc>false</dnc>" +
                    "<ident>" + Email + "</ident>" +
                    "<contactType>BillTo</contactType>" +
                    "</commMode>";
                }

                if (Mobile != "")
                {
                    requestXml = requestXml +
                    "<commMode>" + "<commTypeNo>2</commTypeNo>" +
                    // "<contactNo></contactNo>" +     //  ----- bill phone
                    "<isDefault>false</isDefault>" +
                    "<dnc>false</dnc>" +
                    "<ident>" + Mobile + "</ident>" +
                    "<contactType>BillTo</contactType>" +
                    "</commMode>";
                }

                if (Facebook != "")
                {
                    requestXml = requestXml +
                    "<commMode>" +
                    "<commTypeNo>8</commTypeNo>" +
                    //"<contactNo></contactNo>" +     //  ----- bill to fb
                    "<isDefault>false</isDefault>" +
                    "<dnc>false</dnc>" +
                    "<ident>" + Facebook + "</ident>" +
                    "<contactType>BillTo</contactType>" +
                    "</commMode>";
                }

                if (Twitter != "")
                {
                    requestXml = requestXml +
                    "<commMode>" +
                    "<commTypeNo>7</commTypeNo>" +
                    //"<contactNo></contactNo>" +     //  ----- bill to twitter
                    "<isDefault>false</isDefault>" +
                    "<dnc>false</dnc>" +
                    "<ident>" + Twitter + "</ident>" +
                    "<contactType>BillTo</contactType>" +
                    "</commMode>";
                }
                #endregion

                #region Ship to communication
                if (Email != "")
                {
                    requestXml = requestXml +
                    "<commMode>" +
                    "<commTypeNo>4</commTypeNo>" +
                    //"<contactNo></contactNo>" +     //  ----- ship to email
                    "<isDefault>false</isDefault>" +
                    "<dnc>false</dnc>" +
                    "<ident>" + Siteentity.GetAttributeValue<String>("onl_customeremailaddress") + "</ident>" +
                    "<contactType>ShipTo</contactType>" +
                    "</commMode>";
                }

                if (Mobile != "")
                {
                    requestXml = requestXml +
                    "<commMode>" +
                    "<commTypeNo>2</commTypeNo>" +
                    //"<contactNo></contactNo>" +     //  ----- ship to phone
                    "<isDefault>false</isDefault>" +
                    "<dnc>false</dnc>" +
                    "<ident>" + Siteentity.GetAttributeValue<String>("onl_ol_customercontactnumber") + "</ident>" +
                    "<contactType>ShipTo</contactType>" +
                    "</commMode>";
                }
                #endregion

                #region Ledger request 
                requestXml = requestXml +
                "<LedgerRequest>" +
                "<coaGroupNo>" + coaGroupNo + "</coaGroupNo>" +
                "<coaNo>" + coaNo + "</coaNo>" +
                "<currencyISO>INR</currencyISO>" +
                "<ledgerAccountTypeNo>3</ledgerAccountTypeNo>" +
                "<ledgerBookNo>" + ledgerBookNo + "</ledgerBookNo>" +
                "<name>" + childName + "</name>" +
                "<parentId>" + ledgerParentId + "</parentId>" +

                "<paymentAmnt>" + paymentAmount + "</paymentAmnt>" +
                "<securityAmnt>" + securityAmount + "</securityAmnt>" +
                "<paymentType>" + paymentType + "</paymentType>" +
                "<depositType>" + securityDepositType + "</depositType>" +
                "<paymentDate>" + paymentDate + "</paymentDate>" +
                "<chequeNo>" + chequeNo + "</chequeNo>" +
                "<bankNo>" + bankNo + "</bankNo>" +
                "<bankBranch>" + banBranch + "</bankBranch>" +
                "<gst>" + Gst + "</gst>" +
                "<pan>" + Pan + "</pan>" +
                "<tan>" + Tan + "</tan>" +
                "</LedgerRequest>" +
                #endregion

                #region serviceGroup 
                                "<serviceGroup>" +
                "<actcat>2</actcat>" +
                "<actid>" + CanNo + "</actid>" +
                "<actname>" + childName + "</actname>" +
                "<domno>" + childDomainId + "</domno>" +
                "<placeOfConsumptionId>" + Rev.GetAttributeValue<int>("alletech_placeofconsumptionid").ToString() + "</placeOfConsumptionId>" +
                "</serviceGroup>" +
                #endregion

                #region SessionObject
                    "<SessionObject>" +
                "<credentialId>1</credentialId>" +
                "<ipAddress>22.23.25.12</ipAddress>" +
                "<source>CRM</source>" +
                "<userName>crm.admin</userName>" +
                "<userType>123</userType>" +
                "<usrNo>10651</usrNo>" +
                "</SessionObject>" +
                "</CRMCAFRequest>";
                #endregion



                #endregion
                //replace & with &amp
                if (requestXml.Contains("&"))
                    requestXml = requestXml.Replace("&", "&amp;");


                var B2Buri = new Uri(this.GetValueForKey("SdnJBossUri"));
                var uri = B2Buri;

                Byte[] requestByte = Encoding.UTF8.GetBytes(requestXml);

                WebRequest request = WebRequest.Create(uri);
                request.Method = WebRequestMethods.Http.Post;
                request.ContentLength = requestByte.Length;
                request.ContentType = "text/xml; encoding='utf-8'";
                request.GetRequestStream().Write(requestByte, 0, requestByte.Length);

                #region Response validation
                try
                {
                    if (context.Depth == 1)
                    {
                        using (var response = request.GetResponse())
                        {
                            XmlDocument xmlDoc = new XmlDocument();
                            xmlDoc.Load(response.GetResponseStream());
                            string tmp = xmlDoc.InnerXml.ToString();

                            string CAF_No, CAN_No, Code, Message;
                            #region To create Integration Log from Response
                            XmlNodeList node1 = xmlDoc.GetElementsByTagName("CRMCAFResponse");
                            for (int i = 0; i <= node1.Count - 1; i++)
                            {
                                CAF_No = node1[i].ChildNodes.Item(0).InnerText.Trim();
                                CAN_No = node1[i].ChildNodes.Item(1).InnerText.Trim();
                                Code = node1[i].ChildNodes.Item(2).InnerText.Trim();
                                Message = node1[i].ChildNodes.Item(3).InnerText.Trim();
                                Entity IntegrationLog = new Entity("alletech_integrationlog_enterprise");
                                IntegrationLog["alletech_cafno"] = CAF_No;
                                IntegrationLog["alletech_canno"] = CAN_No;
                                IntegrationLog["alletech_code"] = Code;
                                IntegrationLog["alletech_message"] = Message;

                                IntegrationLog["alletech_name"] = "Approved_" + CAF_No + "_" + CAN_No;

                                IntegrationLog["alletech_responsetype"] = new OptionSetValue(2);
                                IntegrationLog["alletech_approvalrequest"] = requestXml;
                                Guid Siteguid = Siteentity.Id;
                                Guid safid = SAF.Id;
                                //add site lookup 
                                IntegrationLog["spectra_siteidid"] = new EntityReference("onl_customersite", Siteguid);


                                IntegrationLog["alletech_can"] = new EntityReference("account", pacid);//update can on behalf of account
                                IntegrationLog["onl_safid"] = new EntityReference("onl_saf", safid);
                                service.Create(IntegrationLog);
                                Branchflag = true;
                            }
                            #endregion
                        }
                        if (Branchflag == true)
                        {
                            #region Update Customer Site Details 
                            Entity Sites = service.Retrieve("onl_customersite", Siteentity.Id, new ColumnSet("spectra_parentaccout", "spectra_siteaccountno", "spectra_siteresponse", "spectra_siteuserpassword", "onl_account"));

                            //ConditionExpression conditionaccc = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, CanNo);
                            //FilterExpression filteraccc = new FilterExpression();
                            //filteraccc.AddCondition(conditionaccc);
                            //filteraccc.FilterOperator = LogicalOperator.And;

                            //QueryExpression queryaccc = new QueryExpression
                            //{
                            //    EntityName = "account",
                            //    ColumnSet = new ColumnSet("parentaccountid"),
                            //    Criteria = filteraccc,
                            //};
                            //EntityCollection childAccountCC = service.RetrieveMultiple(queryaccc);
                            if (childAccountC.Entities.Count == 0)
                                return;
                            //Entity childPAccountC = childAccountCC.Entities[0];
                           // Guid pacidB = childPAccountC.Id;

                            //Sites["onl_account"] = new EntityReference("account", pacidB);

                            //Guid PcarentAccountID = childPAccountC.GetAttributeValue<EntityReference>("childPAccountC").Id;

                            //Sites["spectra_parentaccout"] = new EntityReference("account", PcarentAccountID);
                            Sites["spectra_siteaccountno"] = CanNo;
                            Sites["spectra_siteresponse"] = "true";
                            Sites["spectra_siteuserpassword"] = CreateRandomPasswordWithRandomLength(8);
                            
                            service.Update(Sites);
                            #endregion
                        }
                        else
                        {
                            Entity Sites = service.Retrieve("onl_customersite", Siteentity.Id, new ColumnSet("spectra_siteaccountno", "spectra_siteresponse"));
                            Sites["spectra_siteaccountno"] = CanNo;
                            Sites["spectra_siteresponse"] = "false";
                            service.Update(Sites);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidPluginExecutionException(ex.Message);
                }
                #endregion

                rowcount = rowcount + 1;
            }
            if (Branchflag == true)
            {
                if (context.Depth == 1)
                {
                    Entity _saf = new Entity("onl_saf");
                    _saf.Id = SAF.Id;
                    _saf["onl_status"] = new OptionSetValue(122050003);
                    // _saf["onl_orgcreationresponseonl"] = true;
                    service.Update(_saf);
                }
            }

            #endregion
        }
        public String ChildcountSitesCreation(IOrganizationService service, ref Entity SAF, Guid Oppid, IPluginExecutionContext context, int sitecount, string ushortname, string Trigger, Entity SiteEntity)
        {
            string Account2 = string.Empty;
            if (context.Depth == 1)
            {
                EntityReference childAccountDomain = null;
                Entity OppEntity = service.Retrieve("opportunity", Oppid, new ColumnSet("alletech_accountid"));
                String canId = String.Empty;
                canId = OppEntity.GetAttributeValue<String>("alletech_accountid");

                tracingService.Trace("Get Account Id based on opportunity id");

                ConditionExpression condition = new ConditionExpression("alletech_accountid", ConditionOperator.Equal, canId);
                FilterExpression filter = new FilterExpression();
                filter.AddCondition(condition);
                filter.FilterOperator = LogicalOperator.And;
                QueryExpression query = new QueryExpression
                {
                    EntityName = "account",
                    ColumnSet = new ColumnSet(true),
                    Criteria = filter,
                };
                EntityCollection childAccountCollection = service.RetrieveMultiple(query);

                Entity childAccount = childAccountCollection.Entities[0];

                if (Trigger == "CreateSiteAccount")
                {
                    // check if Parent account, existing on Child has Unify ID
                    if (childAccount.Attributes.Contains("parentaccountid"))
                    {
                        Entity ChildParentAccount = new Entity("account");

                        EntityReference cityid = SiteEntity.GetAttributeValue<EntityReference>("onl_city");

                        childAccountDomain = retrieveDomain(cityid.Id, service, Productsegmentid);
                        if (childAccountDomain != null)
                        {
                            ChildParentAccount["alletech_domain"] = childAccountDomain;
                            tracingService.Trace("domain : " + childAccountDomain.Id);
                        }
                        //
                        ChildParentAccount["parentaccountid"] = childAccount.GetAttributeValue<EntityReference>("parentaccountid");//new EntityReference("account", parentaid);

                        #region Account Information Details

                        tracingService.Trace("Before Account Info");


                        if (childAccount.Attributes.Contains("alletech_salutation"))
                            ChildParentAccount["alletech_salutation"] = childAccount.GetAttributeValue<OptionSetValue>("alletech_salutation");

                        if (childAccount.Attributes.Contains("alletech_subbusinesssegment"))
                            ChildParentAccount["alletech_subbusinesssegment"] = childAccount.GetAttributeValue<EntityReference>("alletech_subbusinesssegment");

                        if (childAccount.Attributes.Contains("alletech_amountcharged"))
                            ChildParentAccount["alletech_amountcharged"] = childAccount.GetAttributeValue<Money>("alletech_amountcharged");

                        if (childAccount.Attributes.Contains("alletech_businesssegment"))
                            ChildParentAccount["alletech_businesssegment"] = childAccount.GetAttributeValue<EntityReference>("alletech_businesssegment");

                        if (childAccount.Attributes.Contains("alletech_industry"))
                            ChildParentAccount["alletech_industry"] = childAccount.GetAttributeValue<EntityReference>("alletech_industry");

                        if (childAccount.Attributes.Contains("alletech_firmtype"))
                            ChildParentAccount["alletech_firmtype"] = childAccount.GetAttributeValue<OptionSetValue>("alletech_firmtype");

                        if (childAccount.Attributes.Contains("alletech_activationdate"))
                            ChildParentAccount["alletech_activationdate"] = childAccount.GetAttributeValue<DateTime>("alletech_activationdate");

                        if (childAccount.Attributes.Contains("alletech_channelpartner"))
                            ChildParentAccount["alletech_channelpartner"] = childAccount.GetAttributeValue<EntityReference>("alletech_channelpartner");

                        if (childAccount.Attributes.Contains("alletech_product"))
                            ChildParentAccount["alletech_product"] = childAccount.GetAttributeValue<EntityReference>("alletech_product");

                        tracingService.Trace("before s string attributes");

                        if (childAccount.Attributes.Contains("name"))
                            ChildParentAccount["name"] = childAccount.GetAttributeValue<String>("name");

                        string paccshort = ushortname.Split('-')[0];

                        if (childAccount.Attributes.Contains("alletech_unifyshortname"))
                            ChildParentAccount["alletech_unifyshortname"] = paccshort + "-0" + sitecount.ToString();

                        if (childAccount.Attributes.Contains("alletech_accountshortname"))
                            ChildParentAccount["alletech_accountshortname"] = childAccount.GetAttributeValue<String>("alletech_accountshortname");

                        if (childAccount.Attributes.Contains("emailaddress1"))
                            ChildParentAccount["emailaddress1"] = childAccount.GetAttributeValue<String>("emailaddress1");

                        if (childAccount.Attributes.Contains("alletech_transactionid"))
                            ChildParentAccount["alletech_transactionid"] = childAccount.GetAttributeValue<String>("alletech_transactionid");

                        if (SiteEntity.Attributes.Contains("onl_customeremailaddress"))
                            ChildParentAccount["alletech_emailid"] = SiteEntity.GetAttributeValue<String>("onl_customeremailaddress"); //"goodmorningg@xxxxxxx.nnnn";//childAccount.GetAttributeValue<String>("alletech_emailid");

                        if (childAccount.Attributes.Contains("alletech_companyname"))
                            ChildParentAccount["alletech_companyname"] = childAccount.GetAttributeValue<String>("alletech_companyname");

                        if (childAccount.Attributes.Contains("alletech_facebookid"))
                            ChildParentAccount["alletech_facebookid"] = childAccount.GetAttributeValue<String>("alletech_facebookid");

                        if (childAccount.Attributes.Contains("alletech_companynamehome"))
                            ChildParentAccount["alletech_companynamehome"] = childAccount.GetAttributeValue<String>("alletech_companynamehome");

                        if (childAccount.Attributes.Contains("alletech_twitterid"))
                            ChildParentAccount["alletech_twitterid"] = childAccount.GetAttributeValue<String>("alletech_twitterid");

                        if (childAccount.Attributes.Contains("alletech_paymentid"))
                            ChildParentAccount["alletech_paymentid"] = childAccount.GetAttributeValue<String>("alletech_paymentid");

                        if (SiteEntity.Attributes.Contains("onl_ol_customercontactnumber"))
                            ChildParentAccount["alletech_mobilephone"] = SiteEntity.GetAttributeValue<String>("onl_ol_customercontactnumber");

                        if (SiteEntity.Attributes.Contains("onl_customeremergencycontactnumber"))
                            ChildParentAccount["telephone1"] = SiteEntity.GetAttributeValue<String>("onl_customeremergencycontactnumber");

                        if (childAccount.Attributes.Contains("websiteurl"))
                            ChildParentAccount["websiteurl"] = childAccount.GetAttributeValue<String>("websiteurl");

                        //Added by Saurabh
                        if (SiteEntity.Attributes.Contains("onl_address"))
                            ChildParentAccount["alletech_address"] = SiteEntity.GetAttributeValue<String>("onl_address");

                        if (SiteEntity.Attributes.Contains("onl_state"))
                            ChildParentAccount["alletech_state"] = SiteEntity.GetAttributeValue<EntityReference>("onl_state");

                        Guid stateID = SiteEntity.GetAttributeValue<EntityReference>("onl_state").Id;

                        var state = service.Retrieve("alletech_state", stateID, new ColumnSet("alletech_country"));
                        tracingService.Trace("State Retrive Sucessfuly");

                        ChildParentAccount["alletech_country"] = state.GetAttributeValue<EntityReference>("alletech_country");

                        tracingService.Trace("after site string attributes");
                        #endregion
                        string firstName = string.Empty;
                        string lastnameAccShip = string.Empty;
                        var sitename = SiteEntity.GetAttributeValue<String>("spectra_customername");
                        string[] ssizefullnmae = sitename.Split(null);
                        if (ssizefullnmae.Length > 1)
                        {
                            firstName = ssizefullnmae[0];
                            for (int k = 1; k < ssizefullnmae.Length; k++)
                            {
                                lastnameAccShip = lastnameAccShip + " " + ssizefullnmae[k];
                            }
                        }
                        else
                            firstName = ssizefullnmae[0];

                        if (childAccount.Attributes.Contains("alletech_contactfirstname"))
                            ChildParentAccount["alletech_contactfirstname"] = firstName;
                        if (childAccount.Attributes.Contains("alletech_contactlastname"))
                            ChildParentAccount["alletech_contactlastname"] = lastnameAccShip;
                        #region Account Address
                        tracingService.Trace(" Get Site First Name:" + firstName);
                        tracingService.Trace(" Get Site last Name:" + lastnameAccShip);
                        tracingService.Trace(" In site Account Address attributes");


                        tracingService.Trace("after site newly added code in Account address");

                        tracingService.Trace(" Get Site State Name:" + SiteEntity.GetAttributeValue<EntityReference>("onl_state").Name);
                        tracingService.Trace(" Get Site State Id:" + SiteEntity.GetAttributeValue<EntityReference>("onl_state").Id);
                        if (SiteEntity.Attributes.Contains("onl_city"))
                            ChildParentAccount["alletech_city"] = SiteEntity.GetAttributeValue<EntityReference>("onl_city");
                        tracingService.Trace(" Get Site City Id:" + SiteEntity.GetAttributeValue<EntityReference>("onl_city").Id);
                        tracingService.Trace(" Get Site City Name:" + SiteEntity.GetAttributeValue<EntityReference>("onl_city").Name);
                        if (SiteEntity.Attributes.Contains("spectra_pincode"))
                            ChildParentAccount["alletech_zippostalcode"] = SiteEntity.GetAttributeValue<String>("spectra_pincode");

                        tracingService.Trace(" Get Site Zip Code:" + SiteEntity.GetAttributeValue<String>("spectra_pincode"));

                        #endregion

                        #region Bill Address

                        tracingService.Trace("In bill Address section");
                        #region New Added Code
                        if (childAccount.Attributes.Contains("alletech_contactname"))
                            ChildParentAccount["alletech_contactname"] = childAccount.GetAttributeValue<string>("alletech_contactname");
                        if (childAccount.Attributes.Contains("alletech_shippingemailid"))
                            ChildParentAccount["alletech_shippingemailid"] = childAccount.GetAttributeValue<string>("alletech_shippingemailid");
                        if (childAccount.Attributes.Contains("alletech_mobilephone2"))
                            ChildParentAccount["alletech_mobilephone2"] = childAccount.GetAttributeValue<string>("alletech_mobilephone2");
                        #endregion

                        if (childAccount.Attributes.Contains("alletech_bill_countrymain"))
                            ChildParentAccount["alletech_bill_countrymain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_countrymain");
                        if (childAccount.Attributes.Contains("alletech_bill_statemain"))
                            ChildParentAccount["alletech_bill_statemain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_statemain");
                        if (childAccount.Attributes.Contains("alletech_bill_citymain"))
                            ChildParentAccount["alletech_bill_citymain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_citymain");
                        if (childAccount.Attributes.Contains("alletech_bill_pincode"))
                            ChildParentAccount["alletech_bill_pincode"] = childAccount.GetAttributeValue<String>("alletech_bill_pincode");
                        if (childAccount.Attributes.Contains("alletech_bill_areamain"))
                            ChildParentAccount["alletech_bill_areamain"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_areamain");
                        if (childAccount.Attributes.Contains("alletech_bill_specifybillingarea"))
                            ChildParentAccount["alletech_bill_specifybillingarea"] = childAccount.GetAttributeValue<String>("alletech_bill_specifybillingarea");
                        if (childAccount.Attributes.Contains("alletech_bill_buildingname"))
                            ChildParentAccount["alletech_bill_buildingname"] = childAccount.GetAttributeValue<EntityReference>("alletech_bill_buildingname");
                        if (childAccount.Attributes.Contains("alletech_bill_specifybuilding"))
                            ChildParentAccount["alletech_bill_specifybuilding"] = childAccount.GetAttributeValue<String>("alletech_bill_specifybuilding");
                        if (childAccount.Attributes.Contains("alletech_bill_buildingnoplotno"))
                            ChildParentAccount["alletech_bill_buildingnoplotno"] = childAccount.GetAttributeValue<String>("alletech_bill_buildingnoplotno");
                        if (childAccount.Attributes.Contains("alletech_bill_locality"))
                            ChildParentAccount["alletech_bill_locality"] = childAccount.GetAttributeValue<String>("alletech_bill_locality");
                        if (childAccount.Attributes.Contains("alletech_bill_buildingtype"))
                            ChildParentAccount["alletech_bill_buildingtype"] = childAccount.GetAttributeValue<OptionSetValue>("alletech_bill_buildingtype");
                        if (childAccount.Attributes.Contains("alletech_bill_floor"))
                            ChildParentAccount["alletech_bill_floor"] = childAccount.GetAttributeValue<String>("alletech_bill_floor");
                        if (childAccount.Attributes.Contains("alletech_bill_street"))
                            ChildParentAccount["alletech_bill_street"] = childAccount.GetAttributeValue<String>("alletech_bill_street");
                        if (childAccount.Attributes.Contains("alletech_bill_landmarkifany"))
                            ChildParentAccount["alletech_bill_landmarkifany"] = childAccount.GetAttributeValue<String>("alletech_bill_landmarkifany");
                        if (childAccount.Attributes.Contains("alletech_bill_billingphoneno"))
                            ChildParentAccount["alletech_bill_billingphoneno"] = childAccount.GetAttributeValue<String>("alletech_bill_billingphoneno");
                        #endregion

                        tracingService.Trace("Before account Create");

                        Guid ChildParentAccountId = service.Create(ChildParentAccount);



                        Account2 = GetAccountNo(service, ChildParentAccountId);
                        tracingService.Trace("New Account Id Generated :" + Account2);

                        // Parent Account Association at Child Account
                        #region Parent Account update Shortname


                        //Entity childAccount01 = service.Retrieve("account", ChildParentAccountId, new ColumnSet( "alletech_unifyshortname"));
                        //tracingService.Trace("Update Short name Existing Account :");
                        //string paccshort = ushortname.Split('-')[0];
                        //childAccount01["alletech_unifyshortname"] = paccshort + "-0" + sitecount.ToString();
                        //tracingService.Trace("Update Added in shortname");
                        //service.Update(childAccount01);


                        #endregion
                    }
                }

            }
            return Account2;
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
        private static string CreateRandomPasswordWithRandomLength(int Length)
        {
            // Create a string of characters, numbers, special characters that allowed in the password  
            string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*?_-";
            Random random = new Random();

            char[] chars = new char[Length];
            for (int i = 0; i < Length; i++)
            {
                chars[i] = validChars[random.Next(0, validChars.Length)];
            }
            return new string(chars);
        }
        public static string GetAccountNo(IOrganizationService service, Guid Accountkey)
        {
            string getAccno = string.Empty;
            try
            {

                ConditionExpression condition = new ConditionExpression("accountid", ConditionOperator.Equal, Accountkey);
                FilterExpression filter = new FilterExpression();
                filter.AddCondition(condition);
                filter.FilterOperator = LogicalOperator.And;
                QueryExpression query = new QueryExpression
                {
                    EntityName = "account",
                    ColumnSet = new ColumnSet("alletech_accountid"),
                    Criteria = filter,
                };
                EntityCollection ecAccount = service.RetrieveMultiple(query);
                if (ecAccount.Entities.Count > 0)
                {
                    Entity childAccount = ecAccount.Entities[0];
                    getAccno = childAccount.GetAttributeValue<string>("alletech_accountid");
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            return getAccno;
        }
        public EntityReference retrieveDomain(Guid city, IOrganizationService service, Guid Productsegmentid)
        {
            EntityReference childAccountDomain = null;
            try
            {
                QueryExpression queryConfig = new QueryExpression("alletech_domain");
                queryConfig.ColumnSet = new ColumnSet("alletech_domainid");
                queryConfig.Criteria.AddCondition("pcl_city", ConditionOperator.Equal, city);
                queryConfig.Criteria.AddCondition("spectra_productsegment", ConditionOperator.Equal, Productsegmentid);

                EntityCollection domainCollection = service.RetrieveMultiple(queryConfig);
                if (domainCollection.Entities.Count > 0)
                {
                    tracingService.Trace("Domain record found");
                    childAccountDomain = new EntityReference("alletech_domain", domainCollection.Entities[0].Id);
                }
                else
                    tracingService.Trace("no domain records");
            }
            catch (Exception e)
            {
                tracingService.Trace("UnifyIntegration.IntegrationSAF.retrieveDomain() : Domain Retrieval Failed.." + e);
            }
            return childAccountDomain;
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
