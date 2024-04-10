using Entities.Helper;
using Entities.Models;
using Entities.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Repository;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HoneyBeeAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OEMController : Controller
    {
        private readonly IConfiguration _configuration;

        private IGenericRepository<Customer> _customerRepository;
        private IGenericRepository<OemProd> _oemProductRepository;
        private IGenericRepository<OemCompany> _oemCompanyRepository;
        private IGenericRepository<ProcessLog> _processLogRepository;
        private IGenericRepository<SupProd> _supProductRepository;
        private IGenericRepository<Transaction> _transactionRepository;
        private readonly ILogger<OEMController> _logger;

        public OEMController(
            IConfiguration configuration,
            IGenericRepository<Customer> customerRepository,
            IGenericRepository<OemProd> oemProductRepository,
            IGenericRepository<OemCompany> oemCompanyRepository,
            IGenericRepository<ProcessLog> processLogRepository,
            IGenericRepository<SupProd> supProductRepository,
            IGenericRepository<Transaction> transactionRepository,
            ILogger<OEMController> logger
            )
        {
            _customerRepository = customerRepository;
            _oemProductRepository = oemProductRepository;
            _oemCompanyRepository = oemCompanyRepository;
            _processLogRepository = processLogRepository;
            _supProductRepository = supProductRepository;
            _configuration = configuration;
            _transactionRepository = transactionRepository;
            _logger = logger;
        }

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] Login loginModel)
        {
            var customerList = await _customerRepository.SearchAsync(d => d.OperatorNo == loginModel.OperatorNo && d.Password == loginModel.Password && d.IsActive == true && d.IsDelete == false);

            if (customerList != null && customerList.Count() > 0)
            {
                var customer = customerList.FirstOrDefault();
                var token = GenerateJwtToken(customer.OperatorNo);

                var oem = await _oemCompanyRepository.SearchFirstAsync(d => d.OemId == Convert.ToInt16(customer.OemId));



                CustomerSupplierViewModel objModel = new CustomerSupplierViewModel();

                objModel.Id = customer.CId.ToString();
                objModel.Name = oem.OemName;
                objModel.Token = token;
                objModel.OperatorNo = loginModel.OperatorNo;
                objModel.IsSupplier = false;

                return Ok(new Response<CustomerSupplierViewModel>(objModel));

            }
            else
            {
                return Ok(new { Succeeded = false, message = "Please enter valid operator no. and password." });
            }

        }


        #region Customer_Table

        [HttpGet]
        [Route("GetAllOEM")]
        [Authorize]
        public async Task<IActionResult> GetAllCustomers()
        {
            var result = await _customerRepository.SearchAsync(d => d.IsActive == true && d.IsDelete == false);

            return Ok(new Response<IEnumerable<Customer>>(result));

        }

        #endregion Customer_Table

        #region Supplier_Table

        //[HttpGet]
        //[Route("GetAllOE")]
        //public async Task<IActionResult> GetAllSuppliers()
        //{
        //    var result = await _supplierRepository.GetAllAsync();

        //    return Ok(new Response<IEnumerable<Supplier>>(result));

        //}

        [HttpGet]
        [Route("GetAllOEMById/{Id}")]
        public async Task<IActionResult> GetSupplierById(int Id)
        {
            var result = await _oemCompanyRepository.GetByIdAsync(Id);

            return Ok(new Response<OemCompany>(result));

        }

        #endregion Supplier_Table

        #region Transaction_Table

        [HttpGet]
        [Route("GetAllOEMTransactions")]
        public async Task<IActionResult> GetAllOEMTransactions()
        {
            var result = await _transactionRepository.GetAllAsync();

            return Ok(new Response<IEnumerable<Transaction>>(result));

        }

        [HttpGet]
        [Route("GetTransactionById/{Id}")]
        public async Task<IActionResult> GetTransactionById(int Id)
        {
            var result = await _transactionRepository.GetByIdAsync(Id);

            return Ok(new Response<Transaction>(result));

        }

        [HttpPost]
        [Route("AddOrUpdateTransaction")]
        [Authorize]
        //[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "1")]
        public async Task<IActionResult> AddOrUpdateTransaction([FromBody] Transaction tblTrans)
        {
            try
            {
                if (tblTrans is null)
                {
                    return Ok(new { Succeeded = false, message = EnumMessage.ErrorMessageEmptyFromBody });
                }

                /*
                 * This is for Validation Attribute of Order Entity Model
                 */
                if (!ModelState.IsValid)
                {
                    return Ok(new { Succeeded = false, message = EnumMessage.ErrorMessageInvalidModel });
                }

                var prodObj = await _oemProductRepository.SearchFirstAsync(x => x.SpecialVinn0 == tblTrans.SpecialVinno);

                var isDuplicate = await _transactionRepository.DuplicateAsync(x => x.TransId == tblTrans.TransId || x.SerialNo == prodObj.SerialNo || x.ModelNo == prodObj.ModelNo);
                if (isDuplicate)
                {
                    return Ok(new { Succeeded = false, message = EnumMessage.DuplicateRecord });
                }

                var customer = await _customerRepository.SearchFirstAsync(x => x.OperatorNo == tblTrans.OperatorNo);
                if (customer == null)
                {
                    return Ok(new { Succeeded = false, message = "Somthing went wrong. please login again." });
                }
                var oemObj = await _oemCompanyRepository.SearchFirstAsync(x => x.OemId == Convert.ToInt16(customer.OemId));
                
                var tblTransObj = await _transactionRepository.SearchFirstAsync(d => d.TransId == tblTrans.TransId);
                if (tblTransObj == null)
                {
                    tblTransObj = new Transaction
                    {
                        IsActive = true,
                        IsDelete = false,
                        CreateDate = DateTime.Now,
                        CompanyAbbr = oemObj.OemAbbr,
                        OperatorNo = tblTrans.OperatorNo,
                        SubprodItrack = false,
                        HowmanySubparts =String.IsNullOrEmpty(prodObj.SubprodIdlist) ? 0 : prodObj.SubprodIdlist.Split(",").Length,
                        SupprodId = tblTrans.SupprodId,
                        IsthisaSubpart = String.IsNullOrEmpty(prodObj.SubprodIdlist) ? false : (prodObj.SubprodIdlist.Split(",").Length > 0 ? true : false),
                        Scanstatus = true,
                        Transtime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ProdoemId = 1,
                        SpecialVinno = tblTrans.SpecialVinno,
                        ProdNo = prodObj.ProdNo,
                        LotNo = tblTrans.LotNo,
                        BatchNo = tblTrans.BatchNo,
                        ModelNo = prodObj.ModelNo,
                        SerialNo = prodObj.SerialNo,
                        MfgDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        MfgName = prodObj.MfgName,
                        MfgCountry = prodObj.MfgCountry,
                        ProductCuqn = oemObj.OemAbbr + "-" + prodObj.ProdNo + "-" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        QrData = tblTrans.QrData
                    };
                }
                else
                {
                    tblTransObj.ProdNo = tblTrans.ProdNo;
                    tblTransObj.ModelNo = tblTrans.ModelNo;
                    tblTransObj.SerialNo = tblTrans.SerialNo;
                    tblTransObj.MfgName = tblTrans.MfgName;
                    tblTransObj.MfgCountry = tblTrans.MfgCountry;
                    tblTransObj.Ship = tblTrans.Ship;
                    tblTransObj.ModifyDate = DateTime.Now;

                }

                var result = new Transaction();

                if (tblTransObj.TransId == 0)
                {
                    result = await _transactionRepository.AddAsync(tblTransObj);
                }
                else
                {
                    result = await _transactionRepository.UpdateAsync(tblTransObj);
                }

                return Ok(new Response<int>(result.TransId));
                

            }
            catch (Exception ex)
            {
                return BadRequest(new { Succeeded = false, message = ex.Message.ToString() });
            }
        }

        [HttpPost]
        [Route("AddOrUpdateSubProductTransaction")]
        [Authorize]
        //[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "1")]
        public async Task<IActionResult> AddOrUpdateSubProductTransaction([FromBody] ScanOEMSubProduct tblSubProd)
        {
            try
            {
                if (tblSubProd is null)
                {
                    return Ok(new { Succeeded = false, message = EnumMessage.ErrorMessageEmptyFromBody });
                }

                /*
                 * This is for Validation Attribute of Order Entity Model
                 */
                if (!ModelState.IsValid)
                {
                    return Ok(new { Succeeded = false, message = EnumMessage.ErrorMessageInvalidModel });
                }


                var subProd = await _supProductRepository.SearchFirstAsync(x => x.SupprodNo == tblSubProd.supprodNo && x.SupprodName.Trim().Equals(tblSubProd.Name.Trim()) && x.MfgName.Trim().Equals(tblSubProd.mfgName));

                var customer = await _customerRepository.SearchFirstAsync(x => x.OperatorNo == tblSubProd.OperatorNo);
                if (customer == null)
                {
                    return Ok(new { Succeeded = false, message = "Somthing went wrong. please login again." });
                }

                var oemObj = await _oemCompanyRepository.SearchFirstAsync(x => x.OemId == Convert.ToInt16(customer.OemId));
                var prodObj = await _oemProductRepository.SearchFirstAsync(x => x.SpecialVinn0 == tblSubProd.SpecialVinno);

                var tblTrans = await _transactionRepository.SearchFirstAsync(x => x.OperatorNo == tblSubProd.OperatorNo && x.SpecialVinno.Equals(tblSubProd.SpecialVinno) && (x.SupprodId == null || x.SupprodId == 0));

                Transaction tblTransObj;

                var isDuplicate = await _transactionRepository.DuplicateAsync(x => x.OperatorNo == tblSubProd.OperatorNo && x.SpecialVinno.Equals(tblSubProd.SpecialVinno) && x.SupprodId == subProd.SupprodId);
                if (isDuplicate)
                {
                    return Ok(new { Succeeded = false, message = EnumMessage.DuplicateRecord });
                }
                else
                {
                    tblTransObj = new Transaction
                    {
                        IsActive = true,
                        IsDelete = false,
                        CreateDate = DateTime.Now,
                        CompanyAbbr = oemObj.OemAbbr,
                        OperatorNo = tblSubProd.OperatorNo,
                        SubprodItrack = false,
                        HowmanySubparts = 0,
                        SupprodId = subProd.SupprodId,
                        IsthisaSubpart = true,
                        Scanstatus = true,
                        Transtime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ProdoemId = 1,
                        SpecialVinno = tblTrans.SpecialVinno,
                        ProdNo = prodObj.ProdNo,
                        LotNo = tblTrans.LotNo,
                        BatchNo = tblTrans.BatchNo,
                        ModelNo = prodObj.ModelNo,
                        SerialNo = prodObj.SerialNo,
                        MfgDate = DateTime.Now.ToString("yyyy-MM-dd"),
                        MfgName = prodObj.MfgName,
                        MfgCountry = prodObj.MfgCountry,
                        ProductCuqn = oemObj.OemAbbr + "-" + prodObj.ProdNo + "-" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        QrData = tblSubProd.qrData
                    };
                }

                var result = new Transaction();

                if (tblTransObj.TransId == 0)
                {
                    result = await _transactionRepository.AddAsync(tblTransObj);
                }
                else
                {
                    result = await _transactionRepository.UpdateAsync(tblTransObj);
                }

                return Ok(new Response<int>(result.TransId));

            }
            catch (Exception ex)
            {
                return BadRequest(new { Succeeded = false, message = ex.Message.ToString() });
            }
        }


        [HttpDelete]
        [Route("DeleteOEMTransaction/{transId}")]
        public async Task<IActionResult> DeleteOEMTransaction(int transId)
        {
            var tblTrans = await _transactionRepository.GetByIdAsync(transId);

            try
            {
                if (tblTrans != null)
                {
                    tblTrans.IsDelete = true;
                    tblTrans.ModifyDate = DateTime.Now;
                    await _transactionRepository.UpdateAsync(tblTrans);
                }
                else
                {
                    return BadRequest(new
                    {
                        code = 400,
                        status = "Error",
                        message = EnumMessage.DataNotFoundError
                    });
                }
                return Ok(new
                {
                    code = 200,
                    status = "Success",
                    message = EnumMessage.RecordDeleteSuccessfully
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    code = 400,
                    status = "Error",
                    message = ex.Message
                });
            }
        }

        [HttpPut]
        [Route("UpdateQCShippingByVIN")]
        public async Task<IActionResult> UpdateQCShippingByVIN([FromBody] RemoveOEMSubProduct subProdModel)
        {
            var transFlag = await _transactionRepository.SearchFirstAsync(x => x.SpecialVinno.Trim().Equals(subProdModel.SpecialVinno.Trim()) &&
                (x.SupprodId == 0 || x.SupprodId == null) &&
                x.IsActive == true &&
                x.IsDelete == false
            );


            if (transFlag != null && transFlag.TransId > 0)
            {
                if(subProdModel.Type == EnumMessage.QCFlag)
                {
                    transFlag.MovedToQC = subProdModel.MoveValue;
                }
                else
                {
                    transFlag.MovedToShipping = subProdModel.MoveValue;
                }

                var result = await _transactionRepository.UpdateAsync(transFlag);

                return Ok(new Response<int>(result.TransId));

            }
            else
            {
                return BadRequest(new
                {
                    code = 400,
                    status = "Error",
                    message = EnumMessage.DataNotFoundError
                });
            }
        }


        #endregion Transaction_Table


        #region Product

        [HttpGet]
        [Route("GetOEMProductList")]
        public async Task<IActionResult> GetOEMProductList()
        {
            var result = await _oemProductRepository.GetAllAsync();

            return Ok(new Response<IEnumerable<OemProd>>(result));

        }

        [HttpGet]
        [Route("GetOEMProductByVINNo/{VIN}")]
        public async Task<IActionResult> GetOEMProductByVINNo(string VIN)
        {
            var result = await _oemProductRepository.GetAllAsync();

            OemProd oemProd = new OemProd();
            oemProd = result.Where(d => d.SpecialVinn0.Trim().Equals(VIN.Trim())).SingleOrDefault();

            return Ok(new Response<OemProd>(oemProd));

        }

        [HttpGet]
        [Route("GetAllSubProductsByOemProdId/{OemProdId}")]
        public async Task<IActionResult> GetAllSubProductsByOemProdId(int OemProdId)
        {
            var oemProd = await _oemProductRepository.GetByIdAsync(OemProdId);
            var subIdList = oemProd.SubprodIdlist.Split(",");

            List<SupProd> subProdList = new List<SupProd>();

            foreach(var id in subIdList)
            {
                var supProd = await _supProductRepository.GetByIdAsync(Convert.ToInt32(id));

                var trans = await _transactionRepository.SearchFirstAsync(x => x.SupprodId == supProd.SupprodId && x.SpecialVinno == oemProd.SpecialVinn0);
                
                if(trans != null)
                {
                    supProd.isHidden = true;
                }
                else
                {
                    supProd.isHidden = false;
                }

                subProdList.Add(supProd);
            }

            var transFlag = await _transactionRepository.SearchFirstAsync(x => x.SpecialVinno.Trim().Equals(oemProd.SpecialVinn0.Trim()) &&
                x.ProdNo == oemProd.ProdNo &&
                (x.SupprodId == 0 || x.SupprodId == null) &&
                x.IsActive == true &&
                x.IsDelete == false
            );

            string flags = transFlag.MovedToQC.ToString() + "," + transFlag.MovedToShipping.ToString();
            return Ok(new Response<IEnumerable<SupProd>>(subProdList, flags));

        }

        [HttpPut]
        [Route("UpdateOEMSubProductListByVIN")]
        public async Task<IActionResult> UpdateOEMSubProductListByProdId([FromBody] RemoveOEMSubProduct subProdModel)
        {
            var oemProd = await _oemProductRepository.SearchFirstAsync(x => x.SpecialVinn0.Trim().Equals(subProdModel.SpecialVinno.Trim()));
            var supProd = await _supProductRepository.SearchFirstAsync(x => x.SupprodNo.Equals(subProdModel.SupProdNo));

            //if(oemProd != null && oemProd.ProdoemId > 0)
            //{
            //    List<string> ids = oemProd.SubprodIdlist.Split(',').ToList();
            //    ids.Remove(supProd.SupprodId.ToString());

            //    string newIds = string.Join(",", ids);

            //    oemProd.SubprodIdlist = newIds;

            //    var result = await _oemProductRepository.UpdateAsync(oemProd);

            //    return Ok(new Response<int>(result.ProdoemId));

            //    //return Ok(new Response<OemProd>(result));
            //}
            //else
            //{
            //    return BadRequest(new
            //    {
            //        code = 400,
            //        status = "Error",
            //        message = EnumMessage.DataNotFoundError
            //    });
            //}

            //from the transaction list delete the selected subprodid
            var transList = await _transactionRepository.SearchFirstAsync(x => x.SpecialVinno.Trim().Equals(subProdModel.SpecialVinno.Trim()) &&
               x.SupprodId == supProd.SupprodId  &&
               x.ProdNo.Trim().Equals(oemProd.ProdNo.Trim()) &&
               x.HowmanySubparts == 0 &&
               x.IsActive == true &&
               x.IsDelete == false
           );

            if (transList != null)
            {
                await _transactionRepository.DeleteAsync(transList);
                return Ok(new Response<int>(transList.TransId));

            }
            else
            {
                return BadRequest(new
                {
                    code = 400,
                    status = "Error",
                    message = EnumMessage.DataNotFoundError
                });
            }

        }

        [HttpGet]
        [Route("GetQCListByVINNo/{VIN}/{OperatorNo}")]
        public async Task<IActionResult> GetQCListByVINNo(string VIN, string OperatorNo)
        {
            var transList = await _transactionRepository.SearchAsync(x => x.SpecialVinno.Trim().Equals(VIN.Trim()) && 
                x.OperatorNo.Trim().Equals(OperatorNo.Trim()) &&
                x.HowmanySubparts == 0 &&
                x.IsActive == true &&
                x.IsDelete == false 
            );

            if(transList.Count() > 0)
            {
                string prodNo = transList.ToList().FirstOrDefault().ProdNo;

                var oemProd = await _oemProductRepository.SearchFirstAsync(x => x.ProdNo.Equals(prodNo) && x.SpecialVinn0.Trim().Equals(VIN.Trim()));
                List<string> ids = oemProd.SubprodIdlist.Split(',').ToList();

                List<SupProd> subProdList = new List<SupProd>();

                foreach (var item in transList)
                {
                    ids.Remove(item.SupprodId.ToString());
                    var supProd = await _supProductRepository.GetByIdAsync(Convert.ToInt32(item.SupprodId));
                    supProd.isHidden = true;
                    subProdList.Add(supProd);
                }

                //to display empty sub-prod list 
                if(ids.Count() > 0)
                {
                    foreach (var id in ids)
                    {
                        var supProd = await _supProductRepository.GetByIdAsync(Convert.ToInt32(id));
                        supProd.isHidden = false;
                        subProdList.Add(supProd);
                    }
                }

                var prod = await _oemProductRepository.SearchFirstAsync(x => x.SpecialVinn0.Trim().Equals(VIN.Trim()));
                    
                return Ok(new Response<IEnumerable<SupProd>>(subProdList, prod.ProdName));
            }
            else
            {
                return Ok(new Response<IEnumerable<SupProd>>(new List<SupProd>()));
            }
            

        }

        #endregion



        private string GenerateJwtToken(string user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Jwt:ExpiresInMinutes"])),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}
