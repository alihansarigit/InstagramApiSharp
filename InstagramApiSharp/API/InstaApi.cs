﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using InstagramApiSharp.API.Processors;
using InstagramApiSharp.Classes;
using InstagramApiSharp.Classes.Android.DeviceInfo;
using InstagramApiSharp.Classes.Models;
using InstagramApiSharp.Classes.ResponseWrappers;
using InstagramApiSharp.Classes.ResponseWrappers.BaseResponse;
using InstagramApiSharp.Converters;
using InstagramApiSharp.Helpers;
using InstagramApiSharp.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InstagramApiSharp.API
{
    internal class InstaApi : IInstaApi
    {
        private readonly IHttpRequestProcessor _httpRequestProcessor;
        private readonly IInstaLogger _logger;
        private ICollectionProcessor _collectionProcessor;
        private ICommentProcessor _commentProcessor;
        private AndroidDevice _deviceInfo;
        private IFeedProcessor _feedProcessor;

        private IHashtagProcessor _hashtagProcessor;
        private ILocationProcessor _locationProcessor;
        private IMediaProcessor _mediaProcessor;
        private IMessagingProcessor _messagingProcessor;
        private IStoryProcessor _storyProcessor;
        private TwoFactorLoginInfo _twoFactorInfo;
        private InstaChallengeLoginInfo _challengeinfo;
        private UserSessionData _userSession;
        private UserSessionData _user
        {
            get { return _userSession; }
            set { _userSession = value; _userAuthValidate.User = value; }
        }
        private UserAuthValidate _userAuthValidate;
        private IUserProcessor _userProcessor;

        private ILiveProcessor _liveProcessor;
        /// <summary>
        ///     Live api functions.
        /// </summary>
        public ILiveProcessor LiveProcessor => _liveProcessor;

        private IDiscoverProcessor _discoverProcessor;
        /// <summary>
        ///     Discover api functions.
        /// </summary>
        public IDiscoverProcessor DiscoverProcessor => _discoverProcessor;

        private IAccountProcessor _accountProcessor;
        /// <summary>
        ///     Account api functions.
        /// </summary>
        public IAccountProcessor AccountProcessor => _accountProcessor;
        /// <summary>
        ///     Comments api functions.
        /// </summary>
        public ICommentProcessor CommentProcessor => _commentProcessor;
        /// <summary>
        ///     Story api functions.
        /// </summary>
        public IStoryProcessor StoryProcessor => _storyProcessor;
        /// <summary>
        ///     Media api functions.
        /// </summary>
        public IMediaProcessor MediaProcessor => _mediaProcessor;
        /// <summary>
        ///     Messaging (direct) api functions.
        /// </summary>
        public IMessagingProcessor MessagingProcessor => _messagingProcessor;
        /// <summary>
        ///     Feed api functions.
        /// </summary>
        public IFeedProcessor FeedProcessor => _feedProcessor;
        /// <summary>
        ///     Collection api functions.
        /// </summary>
        public ICollectionProcessor CollectionProcessor => _collectionProcessor;
        /// <summary>
        /// Location api functions.
        /// </summary>
        public ILocationProcessor LocationProcessor => _locationProcessor;
        /// <summary>
        ///     Hashtag api functions.
        /// </summary>
        public IHashtagProcessor HashtagProcessor => _hashtagProcessor;
        public IUserProcessor UserProcessor => _userProcessor;
        public InstaApi(UserSessionData user, IInstaLogger logger, AndroidDevice deviceInfo,
            IHttpRequestProcessor httpRequestProcessor)
        {
            _userAuthValidate = new UserAuthValidate();
            _user = user;
            _logger = logger;
            _deviceInfo = deviceInfo;
            _httpRequestProcessor = httpRequestProcessor;
        }

        public UserSessionData GetLoggedUser()
        {
            return _user;
        }
        /// <summary>
        ///     Get currently logged in user info asynchronously
        /// </summary>
        /// <returns>
        ///     <see cref="T:InstagramApiSharp.Classes.Models.InstaCurrentUser" />
        /// </returns>
        public async Task<IResult<InstaCurrentUser>> GetCurrentUserAsync()
        {
            ValidateUser();
            ValidateLoggedIn();
            return await _userProcessor.GetCurrentUserAsync();
        }
        #region Authentication/State data
        private bool _isUserAuthenticated;
        /// <summary>
        ///     Indicates whether user authenticated or not
        /// </summary>
        public bool IsUserAuthenticated
        {
            get { return _isUserAuthenticated; }
            internal set { _isUserAuthenticated = value; _userAuthValidate.IsUserAuthenticated = value; }
        }
        #region Register new account with Phone number and email
        string _waterfallIdReg = "", _deviceIdReg = "", _phoneIdReg = "", _guidReg = "";
        /// <summary>
        ///     Check email availability
        /// </summary>
        /// <param name="email">Email to check</param>
        public async Task<IResult<CheckEmailRegistration>> CheckEmailAsync(string email)
        {
            try
            {
                _waterfallIdReg = Guid.NewGuid().ToString();
                var firstResponse = await _httpRequestProcessor.GetAsync(_httpRequestProcessor.Client.BaseAddress);
                var cookies = 
                    _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(_httpRequestProcessor.Client
                    .BaseAddress);
                var csrftoken = cookies[InstaApiConstants.CSRFTOKEN]?.Value ?? String.Empty;
                _user.CsrfToken = csrftoken;
                Debug.WriteLine("verify token: " + csrftoken);
                var postData = new Dictionary<string, string>
                {
                    {"_csrftoken",      csrftoken},
                    {"login_nonces",    "[]"},
                    {"email",           email},
                    {"qe_id",           Guid.NewGuid().ToString()},
                    {"waterfall_id",    _waterfallIdReg},
                };
                var instaUri = UriCreator.GetCheckEmailUri();
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, postData);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return Result.Fail("Status code: " + response.StatusCode, (CheckEmailRegistration)null);
                }
                else
                {
                    var obj = JsonConvert.DeserializeObject<CheckEmailRegistration>(json);
                    if(obj.ErrorType == "fail")
                        return Result.Fail("Error type: " + obj.ErrorType, (CheckEmailRegistration)null);
                    else if (obj.ErrorType == "email_is_taken")
                        return Result.Fail("Email is taken.", (CheckEmailRegistration)null);
                    else if (obj.ErrorType == "invalid_email")
                        return Result.Fail("Please enter a valid email address.", (CheckEmailRegistration)null);
                    return Result.Success(obj);
                }
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<CheckEmailRegistration>(exception);
            }
        }
        /// <summary>
        ///     Check phone number availability
        /// </summary>
        /// <param name="phoneNumber">Phone number to check</param>
        public async Task<IResult<InstaDefault>> CheckPhoneNumberAsync(string phoneNumber)
        {
            try
            {
                _deviceIdReg = ApiRequestMessage.GenerateDeviceId();
                var firstResponse = await _httpRequestProcessor.GetAsync(_httpRequestProcessor.Client.BaseAddress);
                var cookies =
                    _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(_httpRequestProcessor.Client
                    .BaseAddress);
                var csrftoken = cookies[InstaApiConstants.CSRFTOKEN]?.Value ?? String.Empty;
                _user.CsrfToken = csrftoken;
                Debug.WriteLine("verify token: " + csrftoken);
                var postData = new Dictionary<string, string>
                {
                    {"_csrftoken",      csrftoken},
                    {"login_nonces",    "[]"},
                    {"phone_number",    phoneNumber},
                    {"device_id",    _deviceIdReg},
                };
                var instaUri = UriCreator.GetCheckPhoneNumberUri();
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, postData);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return Result.Fail("Status code: " + response.StatusCode, (InstaDefault)null);
                }
                else
                {
                    var obj = JsonConvert.DeserializeObject<InstaDefault>(json);                 
                    return Result.Success(obj);
                }
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaDefault>(exception);
            }
        }
        /// <summary>
        ///     Send sign up sms code
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        public async Task<IResult<InstaDefault>> SendSignUpSmsCodeAsync(string phoneNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(_deviceIdReg))
                    throw new ArgumentException("You should call CheckPhoneNumberAsync function first.");
                _phoneIdReg = Guid.NewGuid().ToString();
                _waterfallIdReg = Guid.NewGuid().ToString();
                _guidReg = Guid.NewGuid().ToString();
                var cookies =
                    _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(_httpRequestProcessor.Client
                    .BaseAddress);
                var csrftoken = cookies[InstaApiConstants.CSRFTOKEN]?.Value ?? String.Empty;
                _user.CsrfToken = csrftoken;
                var postData = new Dictionary<string, string>
                {
                    {"phone_id",        _phoneIdReg},
                    {"phone_number",    phoneNumber},
                    {"_csrftoken",      csrftoken},
                    {"guid",            _guidReg},
                    {"device_id",       _deviceIdReg},
                    {"waterfall_id",    _waterfallIdReg},
                };
                var instaUri = UriCreator.GetSignUpSMSCodeUri();
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, postData);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var o = JsonConvert.DeserializeObject<AccountRegistrationPhoneNumber>(json);
                    
                    return Result.Fail(o.Message?.Errors?[0], (ResponseType)response.StatusCode, (InstaDefault)null);
                }
                else
                {
                    var obj = JsonConvert.DeserializeObject<InstaDefault>(json);
                    return Result.Success(obj);
                }
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaDefault>(exception);
            }
        }
        /// <summary>
        ///     Verify sign up sms code
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        /// <param name="verificationCode">Verification code</param>
        public async Task<IResult<PhoneNumberRegistration>> VerifySignUpSmsCodeAsync(string phoneNumber, string verificationCode)
        {
            try
            {
                if (string.IsNullOrEmpty(_deviceIdReg))
                    throw new ArgumentException("You should call CheckPhoneNumberAsync function first.");

                if (string.IsNullOrEmpty(_guidReg) || string.IsNullOrEmpty(_waterfallIdReg))
                    throw new ArgumentException("You should call SendSignUpSmsCodeAsync function first.");

                var cookies =
                    _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(_httpRequestProcessor.Client
                    .BaseAddress);
                var csrftoken = cookies[InstaApiConstants.CSRFTOKEN]?.Value ?? String.Empty;
                _user.CsrfToken = csrftoken;
                var postData = new Dictionary<string, string>
                {
                    {"verification_code",         verificationCode},
                    {"phone_number",              phoneNumber},
                    {"_csrftoken",                csrftoken},
                    {"guid",                      _guidReg},
                    {"device_id",                 _deviceIdReg},
                    {"waterfall_id",              _waterfallIdReg},
                };
                var instaUri = UriCreator.GetValidateSignUpSMSCodeUri();
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, postData);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var o = JsonConvert.DeserializeObject<AccountRegistrationPhoneNumberVerifySms>(json);

                    return Result.Fail(o.Errors?.Nonce?[0], (ResponseType)response.StatusCode, (PhoneNumberRegistration)null);
                }
                else
                {
                    var r = JsonConvert.DeserializeObject<AccountRegistrationPhoneNumberVerifySms>(json);
                    if(r.ErrorType == "invalid_nonce")
                        return Result.Fail(r.Errors?.Nonce?[0], (ResponseType)response.StatusCode, (PhoneNumberRegistration)null);

                    await GetRegistrationStepsAsync();
                    var obj = JsonConvert.DeserializeObject<PhoneNumberRegistration>(json);
                    return Result.Success(obj);
                }
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<PhoneNumberRegistration>(exception);
            }
        }
        /// <summary>
        ///     Get username suggestions
        /// </summary>
        /// <param name="name">Name</param>
        public async Task<IResult<RegistrationSuggestionResponse>> GetUsernameSuggestionsAsync(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(_deviceIdReg))
                    _deviceIdReg = ApiRequestMessage.GenerateDeviceId();
                _phoneIdReg = Guid.NewGuid().ToString();
                _waterfallIdReg = Guid.NewGuid().ToString();
                _guidReg = Guid.NewGuid().ToString();
                var cookies =
                    _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(_httpRequestProcessor.Client
                    .BaseAddress);
                var csrftoken = cookies[InstaApiConstants.CSRFTOKEN]?.Value ?? String.Empty;
                _user.CsrfToken = csrftoken;
                var postData = new Dictionary<string, string>
                {
                    {"phone_id",        _phoneIdReg},
                    {"name",            name},
                    {"_csrftoken",      csrftoken},
                    {"guid",            _guidReg},
                    {"device_id",       _deviceIdReg},
                    {"email",           ""},
                    {"waterfall_id",    _waterfallIdReg},
                };
                var instaUri = UriCreator.GetUsernameSuggestionsUri();
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, postData);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var o = JsonConvert.DeserializeObject<AccountRegistrationPhoneNumber>(json);

                    return Result.Fail(o.Message?.Errors?[0], (ResponseType)response.StatusCode, (RegistrationSuggestionResponse)null);
                }
                else
                {
                    var obj = JsonConvert.DeserializeObject<RegistrationSuggestionResponse>(json);
                    return Result.Success(obj);
                }
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<RegistrationSuggestionResponse>(exception);
            }
        }
        /// <summary>
        ///     Validate new account creation with phone number
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        /// <param name="verificationCode">Verification code</param>
        /// <param name="username">Username to set</param>
        /// <param name="password">Password to set</param>
        /// <param name="firstName">First name to set</param>
        public async Task<IResult<AccountCreation>> ValidateNewAccountWithPhoneNumberAsync(string phoneNumber, string verificationCode, string username, string password, string firstName)
        {
            try
            {
                if (string.IsNullOrEmpty(_deviceIdReg))
                    throw new ArgumentException("You should call CheckPhoneNumberAsync function first.");

                if (string.IsNullOrEmpty(_guidReg) || string.IsNullOrEmpty(_waterfallIdReg))
                    throw new ArgumentException("You should call SendSignUpSmsCodeAsync function first.");

                var cookies =
                    _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(_httpRequestProcessor.Client
                    .BaseAddress);
                var csrftoken = cookies[InstaApiConstants.CSRFTOKEN]?.Value ?? String.Empty;
                _user.CsrfToken = csrftoken;
                //sn_nonce:Kzk4OTE3NDMxNDAwNnwxNTM0MTg0MjYzfAhfpJ9rzGNAlQLWQe+kor/nDAntXA0i8Q==
                //+989174314006|1534184263|_��k�c@��A濫��	�\
                //"�
                var postData = new Dictionary<string, string>
                {
                    {"allow_contacts_sync",       "true"},
                    {"verification_code",         verificationCode},
                    {"sn_result",                 "API_ERROR:+null"},
                    {"phone_id",                  _phoneIdReg},
                    {"phone_number",              phoneNumber},
                    {"_csrftoken",                csrftoken},
                    {"username",                  username},
                    {"first_name",                firstName},
                    {"adid",                      Guid.NewGuid().ToString()},
                    {"guid",                      _guidReg},
                    {"device_id",                 _deviceIdReg},
                    {"sn_nonce",                  ""},
                    {"force_sign_up_code",        ""},
                    {"waterfall_id",              _waterfallIdReg},
                    {"qs_stamp",                  ""},
                    {"password",                  password},
                    {"has_sms_consent",           "true"},
                };
                var instaUri = UriCreator.GetCreateValidatedUri();
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, postData);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var o = JsonConvert.DeserializeObject<AccountCreationResponse>(json);

                    return Result.Fail(o.Errors?.Username?[0], (ResponseType)response.StatusCode, (AccountCreation)null);
                }
                else
                {
                    var r = JsonConvert.DeserializeObject<AccountCreationResponse>(json);
                    if (r.ErrorType == "username_is_taken")
                        return Result.Fail(r.Errors?.Username?[0], (ResponseType)response.StatusCode, (AccountCreation)null);

                    var obj = JsonConvert.DeserializeObject<AccountCreation>(json);
                    if (obj.AccountCreated && obj.CreatedUser != null)
                        ValidateUserAsync(obj.CreatedUser, csrftoken, true);
                    return Result.Success(obj);
                }
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<AccountCreation>(exception);
            }
        }


        private async Task<IResult<object>> GetRegistrationStepsAsync()
        {
            try
            {
                var cookies =
                    _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(_httpRequestProcessor.Client
                    .BaseAddress);
                var csrftoken = cookies[InstaApiConstants.CSRFTOKEN]?.Value ?? String.Empty;
                _user.CsrfToken = csrftoken;
                var postData = new Dictionary<string, string>
                {
                    {"fb_connected",            "false"},
                    {"seen_steps",            "[]"},
                    {"phone_id",        _phoneIdReg},
                    {"fb_installed",            "false"},
                    {"locale",            "en_US"},
                    {"timezone_offset",            "16200"},
                    {"network_type",            "WIFI-UNKNOWN"},
                    {"_csrftoken",      csrftoken},
                    {"guid",            _guidReg},
                    {"is_ci",            "false"},
                    {"android_id",       _deviceIdReg},
                    {"reg_flow_taken",           "phone"},
                    {"tos_accepted",    "false"},
                };
                var instaUri = UriCreator.GetOnboardingStepsUri();
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, postData);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var o = JsonConvert.DeserializeObject<AccountRegistrationPhoneNumber>(json);

                    return Result.Fail(o.Message?.Errors?[0], (ResponseType)response.StatusCode, (RegistrationSuggestionResponse)null);
                }
                else
                {
                    var obj = JsonConvert.DeserializeObject<RegistrationSuggestionResponse>(json);
                    return Result.Success(obj);
                }
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<RegistrationSuggestionResponse>(exception);
            }
        }

        /// <summary>
        ///     Create a new instagram account
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="password">Password</param>
        /// <param name="email">Email</param>
        /// <param name="firstName">First name (optional)</param>
        /// <returns></returns>
        public async Task<IResult<AccountCreation>> CreateNewAccountAsync(string username, string password, string email, string firstName)
        {
            AccountCreation createResponse = new AccountCreation();
            try
            {
                var postData = new Dictionary<string, string>
                {
                    {"email",     email },
                    {"username",    username},
                    {"password",    password},
                    {"device_id",   ApiRequestMessage.GenerateDeviceId()},
                    {"guid",        _deviceInfo.DeviceGuid.ToString()},
                    {"first_name",  firstName}
                };

                var instaUri = UriCreator.GetCreateAccountUri();
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, postData);
                var response = await _httpRequestProcessor.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();

                return Result.Success(JsonConvert.DeserializeObject<AccountCreation>(result));
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<AccountCreation>(exception);
            }
        }
        #endregion
        /// <summary>
        ///     Login using given credentials asynchronously
        /// </summary>
        /// <param name="isNewLogin"></param>
        /// <returns>
        ///     Success --> is succeed
        ///     TwoFactorRequired --> requires 2FA login.
        ///     BadPassword --> Password is wrong
        ///     InvalidUser --> User/phone number is wrong
        ///     Exception --> Something wrong happened
        /// </returns>
        public async Task<IResult<InstaLoginResult>> LoginAsync(bool isNewLogin = true)
        {
            ValidateUser();
            ValidateRequestMessage();
            try
            {
                if (isNewLogin)
                {
                    var firstResponse = await _httpRequestProcessor.GetAsync(_httpRequestProcessor.Client.BaseAddress);
                    var html = await firstResponse.Content.ReadAsStringAsync();
                    _logger?.LogResponse(firstResponse);
                }
                var cookies =
                    _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(_httpRequestProcessor.Client
                        .BaseAddress);
              
                var csrftoken = cookies[InstaApiConstants.CSRFTOKEN]?.Value ?? String.Empty;
                _user.CsrfToken = csrftoken;
                Debug.WriteLine("login token: " + csrftoken);
                var instaUri = UriCreator.GetLoginUri();
                var signature =
                    $"{_httpRequestProcessor.RequestMessage.GenerateSignature(InstaApiConstants.IG_SIGNATURE_KEY, out string devid)}.{_httpRequestProcessor.RequestMessage.GetMessageString()}";
                _deviceInfo.DeviceId = devid;
                var fields = new Dictionary<string, string>
                {
                    {InstaApiConstants.HEADER_IG_SIGNATURE, signature},
                    {InstaApiConstants.HEADER_IG_SIGNATURE_KEY_VERSION, InstaApiConstants.IG_SIGNATURE_KEY_VERSION}
                };
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo);
                request.Content = new FormUrlEncodedContent(fields);
                request.Properties.Add(InstaApiConstants.HEADER_IG_SIGNATURE, signature);
                request.Properties.Add(InstaApiConstants.HEADER_IG_SIGNATURE_KEY_VERSION, InstaApiConstants.IG_SIGNATURE_KEY_VERSION);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK) //If the password is correct BUT 2-Factor Authentication is enabled, it will still get a 400 error (bad request)
                {
                    //Then check it
                    var loginFailReason = JsonConvert.DeserializeObject<InstaLoginBaseResponse>(json);

                    if (loginFailReason.InvalidCredentials)
                        return Result.Fail("Invalid Credentials",
                            loginFailReason.ErrorType == "bad_password"
                                ? InstaLoginResult.BadPassword
                                : InstaLoginResult.InvalidUser);
                    if (loginFailReason.TwoFactorRequired)
                    {
                        _twoFactorInfo = loginFailReason.TwoFactorLoginInfo;
                        //2FA is required!
                        return Result.Fail("Two Factor Authentication is required", InstaLoginResult.TwoFactorRequired);
                    }
                    if (loginFailReason.ErrorType == "checkpoint_challenge_required")
                    {
                        _challengeinfo = loginFailReason.Challenge;

                        return Result.Fail("Challenge is required", InstaLoginResult.ChallengeRequired);
                    }
                    if (loginFailReason.ErrorType == "rate_limit_error")
                    {
                        return Result.Fail("Please wait a few minutes before you try again.", InstaLoginResult.LimitError);
                    }
                    return Result.UnExpectedResponse<InstaLoginResult>(response, json);
                }
                var loginInfo = JsonConvert.DeserializeObject<InstaLoginResponse>(json);
                IsUserAuthenticated = loginInfo.User?.UserName.ToLower() == _user.UserName.ToLower();
                var converter = ConvertersFabric.Instance.GetUserShortConverter(loginInfo.User);
                _user.LoggedInUser = converter.Convert();
                _user.RankToken = $"{_user.LoggedInUser.Pk}_{_httpRequestProcessor.RequestMessage.phone_id}";
                if(string.IsNullOrEmpty(_user.CsrfToken))
                {
                    cookies =
                      _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(_httpRequestProcessor.Client
                          .BaseAddress);
                    _user.CsrfToken = cookies[InstaApiConstants.CSRFTOKEN]?.Value ?? String.Empty;
                }
                return Result.Success(InstaLoginResult.Success);
            }
            catch (Exception exception)
            {
                LogException(exception);
                return Result.Fail(exception, InstaLoginResult.Exception);
            }
            finally
            {
                InvalidateProcessors();
            }
        }

        /// <summary>
        ///     2-Factor Authentication Login using a verification code
        ///     Before call this method, please run LoginAsync first.
        /// </summary>
        /// <param name="verificationCode">Verification Code sent to your phone number</param>
        /// <returns>
        ///     Success --> is succeed
        ///     InvalidCode --> The code is invalid
        ///     CodeExpired --> The code is expired, please request a new one.
        ///     Exception --> Something wrong happened
        /// </returns>
        public async Task<IResult<InstaLoginTwoFactorResult>> TwoFactorLoginAsync(string verificationCode)
        {
            if (_twoFactorInfo == null)
                return Result.Fail<InstaLoginTwoFactorResult>("Run LoginAsync first");

            try
            {
                var twoFactorRequestMessage = new ApiTwoFactorRequestMessage(verificationCode,
                    _httpRequestProcessor.RequestMessage.username,
                    _httpRequestProcessor.RequestMessage.device_id,
                    _twoFactorInfo.TwoFactorIdentifier);

                var instaUri = UriCreator.GetTwoFactorLoginUri();
                var signature =
                    $"{twoFactorRequestMessage.GenerateSignature(InstaApiConstants.IG_SIGNATURE_KEY)}.{twoFactorRequestMessage.GetMessageString()}";
                var fields = new Dictionary<string, string>
                {
                    {InstaApiConstants.HEADER_IG_SIGNATURE, signature},
                    {
                        InstaApiConstants.HEADER_IG_SIGNATURE_KEY_VERSION,
                        InstaApiConstants.IG_SIGNATURE_KEY_VERSION
                    }
                };
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo);
                request.Content = new FormUrlEncodedContent(fields);
                request.Properties.Add(InstaApiConstants.HEADER_IG_SIGNATURE, signature);
                request.Properties.Add(InstaApiConstants.HEADER_IG_SIGNATURE_KEY_VERSION,
                    InstaApiConstants.IG_SIGNATURE_KEY_VERSION);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var loginInfo =
                        JsonConvert.DeserializeObject<InstaLoginResponse>(json);
                    IsUserAuthenticated = IsUserAuthenticated =
                        loginInfo.User != null && loginInfo.User.UserName.ToLower() == _user.UserName.ToLower();
                    var converter = ConvertersFabric.Instance.GetUserShortConverter(loginInfo.User);
                    _user.LoggedInUser = converter.Convert();
                    _user.RankToken = $"{_user.LoggedInUser.Pk}_{_httpRequestProcessor.RequestMessage.phone_id}";

                    return Result.Success(InstaLoginTwoFactorResult.Success);
                }

                var loginFailReason = JsonConvert.DeserializeObject<InstaLoginTwoFactorBaseResponse>(json);

                if (loginFailReason.ErrorType == "sms_code_validation_code_invalid")
                    return Result.Fail("Please check the security code.", InstaLoginTwoFactorResult.InvalidCode);
                return Result.Fail("This code is no longer valid, please, call LoginAsync again to request a new one",
                    InstaLoginTwoFactorResult.CodeExpired);
            }
            catch (Exception exception)
            {
                LogException(exception);
                return Result.Fail(exception, InstaLoginTwoFactorResult.Exception);
            }
        }

        /// <summary>
        ///     Get Two Factor Authentication details
        /// </summary>
        /// <returns>
        ///     An instance of TwoFactorInfo if success.
        ///     A null reference if not success; in this case, do LoginAsync first and check if Two Factor Authentication is
        ///     required, if not, don't run this method
        /// </returns>
        public async Task<IResult<TwoFactorLoginInfo>> GetTwoFactorInfoAsync()
        {
            return await Task.Run(() =>
                _twoFactorInfo != null
                    ? Result.Success(_twoFactorInfo)
                    : Result.Fail<TwoFactorLoginInfo>("No Two Factor info available."));
        }

        /// <summary>
        ///     Logout from instagram asynchronously
        /// </summary>
        /// <returns>
        ///     True if logged out without errors
        /// </returns>
        public async Task<IResult<bool>> LogoutAsync()
        {
            ValidateUser();
            ValidateLoggedIn();
            try
            {
                var instaUri = UriCreator.GetLogoutUri();
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, instaUri, _deviceInfo);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK) return Result.UnExpectedResponse<bool>(response, json);
                var logoutInfo = JsonConvert.DeserializeObject<BaseStatusResponse>(json);
                if (logoutInfo.Status == "ok")
                    IsUserAuthenticated = false;
                return Result.Success(!IsUserAuthenticated);
            }
            catch (Exception exception)
            {
                LogException(exception);
                return Result.Fail(exception, false);
            }
        }
        ///// <summary>
        /////     Get Challenge information
        ///// </summary>
        ///// <returns></returns>
        //public InstaChallengeLoginInfo GetChallenge()
        //{
        //    return _challengeinfo;
        //}
        string _challengeGuid, _challengeDeviceId;
        public async Task<IResult<ChallengeRequireVerifyMethod>> GetChallengeRequireVerifyMethodAsync()
        {
            if (_challengeinfo == null)
                return Result.Fail("challenge require info is empty.\r\ntry to call LoginAsync function first.", (ChallengeRequireVerifyMethod)null);

            try
            {
                _challengeGuid = Guid.NewGuid().ToString();
                _challengeDeviceId = ApiRequestMessage.GenerateDeviceId();
                var instaUri = UriCreator.GetChallengeRequireFirstUri(_challengeinfo.ApiPath, _challengeGuid, _challengeDeviceId);
                var request = HttpHelper.GetDefaultRequest(HttpMethod.Get, instaUri, _deviceInfo);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var msg = "";
                    try
                    {
                        var j = JsonConvert.DeserializeObject<ChallengeRequireVerifyMethod>(json);
                        msg = j.Message;
                    }
                    catch { }
                    return Result.Fail(msg +"\t\tStatus code: " + response.StatusCode+"\r\n"+ json, (ChallengeRequireVerifyMethod)null);
                }
                else
                {
                    var obj = JsonConvert.DeserializeObject<ChallengeRequireVerifyMethod>(json);
                    return Result.Success(obj);
                }
            }
            catch (Exception ex)
            {
                return Result.Fail(ex, (ChallengeRequireVerifyMethod)null);
            }
        }

        public async Task<IResult<ChallengeRequireVerifyMethod>> ResetChallengeRequireVerifyMethodAsync()
        {
            if (_challengeinfo == null)
                return Result.Fail("challenge require info is empty.\r\ntry to call LoginAsync function first.", (ChallengeRequireVerifyMethod)null);

            try
            {
                _challengeGuid = Guid.NewGuid().ToString();
                _challengeDeviceId = ApiRequestMessage.GenerateDeviceId();
                var instaUri = UriCreator.GetResetChallengeRequireUri(_challengeinfo.ApiPath);
                var data = new JObject
                {
                    {"_csrftoken", _user.CsrfToken},
                    {"guid", _challengeGuid},
                    {"device_id", _challengeDeviceId},
                };
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var msg = "";
                    try
                    {
                        var j = JsonConvert.DeserializeObject<ChallengeRequireVerifyMethod>(json);
                        msg = j.Message;
                    }
                    catch { }
                    return Result.Fail(msg + "\t\tStatus code: " + response.StatusCode + "\r\n" + json, (ChallengeRequireVerifyMethod)null);
                }
                else
                {
                    var obj = JsonConvert.DeserializeObject<ChallengeRequireVerifyMethod>(json);
                    return Result.Success(obj);
                }
            }
            catch (Exception ex)
            {
                return Result.Fail(ex, (ChallengeRequireVerifyMethod)null);
            }
        }

        public async Task<IResult<ChallengeRequireSMSVerify>> RequestVerifyCodeToSMSForChallengeRequireAsync()
        {
            if (_challengeinfo == null)
                return Result.Fail("challenge require info is empty.\r\ntry to call LoginAsync function first.", (ChallengeRequireSMSVerify)null);

            try
            {
                var instaUri = UriCreator.GetChallengeRequireUri(_challengeinfo.ApiPath);
                if (string.IsNullOrEmpty(_challengeGuid))
                    _challengeGuid = Guid.NewGuid().ToString();
                if (string.IsNullOrEmpty(_challengeDeviceId))
                    _challengeDeviceId = ApiRequestMessage.GenerateDeviceId();
                var data = new JObject
                {
                    {"choice", "0"},
                    {"_csrftoken", _user.CsrfToken},
                    {"guid", _challengeGuid},
                    {"device_id", _challengeDeviceId},
                };
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                request.Headers.Add("Host", "i.instagram.com");
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var msg = "";
                    try
                    {
                        var j = JsonConvert.DeserializeObject<ChallengeRequireSMSVerify>(json);
                        msg = j.Message;
                    }
                    catch { }
                    return Result.Fail(msg, (ChallengeRequireSMSVerify)null);
                }
                else
                {
                    var obj = JsonConvert.DeserializeObject<ChallengeRequireSMSVerify>(json);
                    return Result.Success(obj);
                }
            }
            catch (Exception ex)
            {
                return Result.Fail(ex, (ChallengeRequireSMSVerify)null);
            }
        }

        public async Task<IResult<ChallengeRequireEmailVerify>> RequestVerifyCodeToEmailForChallengeRequireAsync()
        {
            if (_challengeinfo == null)
                return Result.Fail("challenge require info is empty.\r\ntry to call LoginAsync function first.", (ChallengeRequireEmailVerify)null);

            try
            {
                var instaUri = UriCreator.GetChallengeRequireUri(_challengeinfo.ApiPath);
                if (string.IsNullOrEmpty(_challengeGuid))
                    _challengeGuid = Guid.NewGuid().ToString();
                if (string.IsNullOrEmpty(_challengeDeviceId))
                    _challengeDeviceId = ApiRequestMessage.GenerateDeviceId();
                var data = new JObject
                {
                    {"choice", "1"},
                    {"_csrftoken", _user.CsrfToken},
                    {"guid", _challengeGuid},
                    {"device_id", _challengeDeviceId},
                };
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                request.Headers.Add("Host", "i.instagram.com");
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var msg = "";
                    try
                    {
                        var j = JsonConvert.DeserializeObject<ChallengeRequireEmailVerify>(json);
                        msg = j.Message;
                    }
                    catch { }
                    return Result.Fail(msg, (ChallengeRequireEmailVerify)null);
                }
                else
                {
                    var obj = JsonConvert.DeserializeObject<ChallengeRequireEmailVerify>(json);
                    return Result.Success(obj);
                }
            }
            catch (Exception ex)
            {
                return Result.Fail(ex, (ChallengeRequireEmailVerify)null);
            }
        }

        public async Task<IResult<ChallengeRequireVerifyCode>> VerifyCodeForChallengeRequireAsync(string verifyCode)
        {
            if(verifyCode.Length != 6)
                return Result.Fail("Verify code must be an 6 digit number.", (ChallengeRequireVerifyCode)null);

            if (_challengeinfo == null)
                return Result.Fail("challenge require info is empty.\r\ntry to call LoginAsync function first.", (ChallengeRequireVerifyCode)null);

            try
            {
                var cookies =
            _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(_httpRequestProcessor.Client
                .BaseAddress);
                var csrftoken = cookies[InstaApiConstants.CSRFTOKEN]?.Value ?? String.Empty;
                _user.CsrfToken = csrftoken;
                Debug.WriteLine("verify token: " + csrftoken);

                var instaUri = UriCreator.GetChallengeRequireUri(_challengeinfo.ApiPath);
                if (string.IsNullOrEmpty(_challengeGuid))
                    _challengeGuid = Guid.NewGuid().ToString();
                if (string.IsNullOrEmpty(_challengeDeviceId))
                    _challengeDeviceId = ApiRequestMessage.GenerateDeviceId();
                var data = new JObject
                {
                    {"security_code", verifyCode},
                    {"_csrftoken", _user.CsrfToken},
                    {"guid", _challengeGuid},
                    {"device_id", _challengeDeviceId},
                };
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                request.Headers.Add("Host", "i.instagram.com");
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    var msg = "";
                    try
                    {
                        var j = JsonConvert.DeserializeObject<ChallengeRequireVerifyCode>(json);
                        msg = j.Message;
                    }
                    catch { }
                    return Result.Fail(msg, (ChallengeRequireVerifyCode)null);
                }
                else
                {
                    var obj = JsonConvert.DeserializeObject<ChallengeRequireVerifyCode>(json);
                    if (obj != null)
                    {
                        if (obj.LoggedInUser != null)
                        {
                            ValidateUserAsync(obj.LoggedInUser, csrftoken);
                            await Task.Delay(1500);
                            await _messagingProcessor.GetDirectInboxAsync();
                            await _feedProcessor.GetRecentActivityFeedAsync(PaginationParameters.MaxPagesToLoad(1));
                        }
                        else
                        {

                        }
                    }
                    

                    return Result.Success(obj);
                }
            }
            catch (Exception ex)
            {
                return Result.Fail(ex, (ChallengeRequireVerifyCode)null);
            }
        }


        private void ValidateUserAsync(InstaUserShortResponse user, string csrfToken, bool validateExtra = true)
        {
            try
            {
                var converter = ConvertersFabric.Instance.GetUserShortConverter(user);
                _user.LoggedInUser = converter.Convert();
                if (validateExtra)
                {
                    _user.RankToken = $"{_user.LoggedInUser.Pk}_{_httpRequestProcessor.RequestMessage.phone_id}";
                    _user.CsrfToken = csrfToken;
                    IsUserAuthenticated = true;
                    InvalidateProcessors();
                }
            }
            catch { }
        }
        /// <summary>
        ///     Set cookie and html document to verify login information.
        /// </summary>
        /// <param name="htmlDocument">Html document source</param>
        /// <param name="cookies">Cookies from webview or webbrowser control</param>
        /// <returns>True if logged in, False if not</returns>
        public async Task<IResult<bool>> SetCookiesAndHtmlForFacebookLoginAsync(string htmlDocument, string cookie, bool facebookLogin = false)
        {
            if (!string.IsNullOrEmpty(cookie) && !string.IsNullOrEmpty(htmlDocument))
            {
                try
                {
                    var start = "<script type=\"text/javascript\">window._sharedData";
                    var end = ";</script>";

                    var str = htmlDocument.Substring(htmlDocument.IndexOf(start) + start.Length);
                    str = str.Substring(0, str.IndexOf(end));
                    str = str.Substring(str.IndexOf("=") + 2);
                    var o = JsonConvert.DeserializeObject<WebBrowserResponse>(str);
                    return await SetCookiesAndHtmlForFacebookLogin(o, cookie, facebookLogin);
                }
                catch (Exception ex)
                {
                    return Result.Fail(ex.Message, false);
                }
            }
            return Result.Fail("", false);
        }
        /// <summary>
        ///     Set cookie and web browser response object to verify login information.
        /// </summary>
        /// <param name="webBrowserResponse">Web browser response object</param>
        /// <param name="cookies">Cookies from webview or webbrowser control</param>
        /// <returns>True if logged in, False if not</returns>
        public async Task<IResult<bool>> SetCookiesAndHtmlForFacebookLogin(WebBrowserResponse webBrowserResponse, string cookie, bool facebookLogin = false)
        {
            if(webBrowserResponse == null)
                return Result.Fail("", false);
            if(webBrowserResponse.Config == null)
                return Result.Fail("", false);
            if(webBrowserResponse.Config.Viewer == null)
                return Result.Fail("", false);

            if (!string.IsNullOrEmpty(cookie))
            {
                try
                {
                    var uri = new Uri(InstaApiConstants.INSTAGRAM_URL);
                    //if (cookie.Contains("urlgen"))
                    //{
                    //    var removeStart = "urlgen=";
                    //    var removeEnd = ";";
                    //    var t = cookie.Substring(cookie.IndexOf(removeStart) + 0);
                    //    t = t.Substring(0, t.IndexOf(removeEnd) + 2);
                    //    cookie = cookie.Replace(t, "");
                    //}
                    cookie = cookie.Replace(';', ',');
                    _httpRequestProcessor.HttpHandler.CookieContainer.SetCookies(uri, cookie);

                    InstaUserShort user = new InstaUserShort
                    {
                        Pk = long.Parse(webBrowserResponse.Config.Viewer.Id),
                        UserName = _user.UserName,
                        ProfilePictureId = "unknown",
                        FullName = webBrowserResponse.Config.Viewer.FullName,
                        ProfilePicture = webBrowserResponse.Config.Viewer.ProfilePicUrl
                    };
                    _user.LoggedInUser = user;
                    _user.CsrfToken = webBrowserResponse.Config.CsrfToken;
                    _user.RankToken = $"{webBrowserResponse.Config.Viewer.Id}_{_httpRequestProcessor.RequestMessage.phone_id}";
                    IsUserAuthenticated = true;
                    if (facebookLogin)
                    {
                        try
                        {
                            var instaUri = UriCreator.GetFacebookSignUpUri();
                            var data = new JObject
                            {
                                {"dryrun", "true"},
                                {"phone_id", _deviceInfo.DeviceGuid.ToString()},
                                {"_csrftoken", _user.CsrfToken},
                                {"adid", Guid.NewGuid().ToString()},
                                {"guid", Guid.NewGuid().ToString()},
                                {"device_id", ApiRequestMessage.GenerateDeviceId()},
                                {"waterfall_id", Guid.NewGuid().ToString()},
                                {"fb_access_token", InstaApiConstants.FB_ACCESS_TOKEN},
                            };
                            var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                            request.Headers.Add("Host", "i.instagram.com");
                            var response = await _httpRequestProcessor.SendAsync(request);
                            var json = await response.Content.ReadAsStringAsync();
                            var obj = JsonConvert.DeserializeObject<FacebookLoginResponse>(json);
                            _user.FacebookUserId = obj.FbUserId;
                        }
                        catch(Exception)
                        {
                        }
                        InvalidateProcessors();
                    }
                    return Result.Success(true);
                }
                catch (Exception ex)
                {
                    return Result.Fail(ex.Message, false);
                }
            }
            return Result.Fail("", false);
        }
        /// <summary>
        ///     Get current state info as Memory stream
        /// </summary>
        /// <returns>
        ///     State data
        /// </returns>
        public Stream GetStateDataAsStream()
        {

            var Cookies = _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(new Uri(InstaApiConstants.INSTAGRAM_URL));
            var RawCookiesList = new List<Cookie>();
            foreach (Cookie cookie in Cookies)
            {
                RawCookiesList.Add(cookie);
            }


            var state = new StateData
            {
                DeviceInfo = _deviceInfo,
                IsAuthenticated = IsUserAuthenticated,
                UserSession = _user,
                Cookies = _httpRequestProcessor.HttpHandler.CookieContainer,
                RawCookies = RawCookiesList
            };
            return SerializationHelper.SerializeToStream(state);
        }
        /// <summary>
        ///     Get current state info as Memory stream
        /// </summary>
        /// <returns>
        ///     State data
        /// </returns>
        public string GetStateDataAsString()
        {

            var Cookies = _httpRequestProcessor.HttpHandler.CookieContainer.GetCookies(new Uri(InstaApiConstants.INSTAGRAM_URL));
            var RawCookiesList = new List<Cookie>();
            foreach (Cookie cookie in Cookies)
            {
                RawCookiesList.Add(cookie);
            }

            var state = new StateData
            {
                DeviceInfo = _deviceInfo,
                IsAuthenticated = IsUserAuthenticated,
                UserSession = _user,
                Cookies = _httpRequestProcessor.HttpHandler.CookieContainer,
                RawCookies = RawCookiesList
            };
            return SerializationHelper.SerializeToString(state);
        }

        /// <summary>
        ///     Loads the state data from stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        public void LoadStateDataFromStream(Stream stream)
        {
            var data = SerializationHelper.DeserializeFromStream<StateData>(stream);
            _deviceInfo = data.DeviceInfo;
            _user = data.UserSession;
            // _httpRequestProcessor.HttpHandler.CookieContainer = data.Cookies;

            //Load Stream Edit 
            _httpRequestProcessor.RequestMessage.username = data.UserSession.UserName;
            _httpRequestProcessor.RequestMessage.password = data.UserSession.Password;
            // _httpRequestProcessor.HttpHandler.CookieContainer = data.Cookies;
            _httpRequestProcessor.RequestMessage.device_id = data.DeviceInfo.DeviceId;
            _httpRequestProcessor.RequestMessage.phone_id = data.DeviceInfo.PhoneGuid.ToString();
            _httpRequestProcessor.RequestMessage.guid = data.DeviceInfo.DeviceGuid;

            foreach (Cookie cookie in data.RawCookies)
            {
                _httpRequestProcessor.HttpHandler.CookieContainer.Add(new Uri(InstaApiConstants.INSTAGRAM_URL), cookie);
            }

            //_httpRequestProcessor.HttpHandler.CookieContainer.SetCookies(new Uri(InstaApiConstants.INSTAGRAM_URL),data.RawCookies);



            IsUserAuthenticated = data.IsAuthenticated;
            InvalidateProcessors();
        }
        public void LoadStateDataFromString(string str)
        {
            var data = SerializationHelper.DeserializeFromString<StateData>(str);
            _deviceInfo = data.DeviceInfo;
            _user = data.UserSession;
            // _httpRequestProcessor.HttpHandler.CookieContainer = data.Cookies;

            //Load Stream Edit 
            _httpRequestProcessor.RequestMessage.username = data.UserSession.UserName;
            _httpRequestProcessor.RequestMessage.password = data.UserSession.Password;
            // _httpRequestProcessor.HttpHandler.CookieContainer = data.Cookies;
            _httpRequestProcessor.RequestMessage.device_id = data.DeviceInfo.DeviceId;
            _httpRequestProcessor.RequestMessage.phone_id = data.DeviceInfo.PhoneGuid.ToString();
            _httpRequestProcessor.RequestMessage.guid = data.DeviceInfo.DeviceGuid;

            foreach (Cookie cookie in data.RawCookies)
            {
                _httpRequestProcessor.HttpHandler.CookieContainer.Add(new Uri(InstaApiConstants.INSTAGRAM_URL), cookie);
            }

            //_httpRequestProcessor.HttpHandler.CookieContainer.SetCookies(new Uri(InstaApiConstants.INSTAGRAM_URL),data.RawCookies);



            IsUserAuthenticated = data.IsAuthenticated;
            InvalidateProcessors();
        }
        #endregion


        #region private part

        private void InvalidateProcessors()
        {
            _hashtagProcessor = new HashtagProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);
            _locationProcessor = new LocationProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);
            _collectionProcessor = new CollectionProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);
            _mediaProcessor = new MediaProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);
            _userProcessor = new UserProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);
            _storyProcessor = new StoryProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);
            _commentProcessor = new CommentProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);
            _messagingProcessor = new MessagingProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);
            _feedProcessor = new FeedProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);

            _liveProcessor = new LiveProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);
            _discoverProcessor = new DiscoverProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);
            _accountProcessor = new AccountProcessor(_deviceInfo, _user, _httpRequestProcessor, _logger, _userAuthValidate);

        }
        internal void ValidateUserAndLogin()
        {
            ValidateUser();
            ValidateLoggedIn();
        }
        private void ValidateUser()
        {
            if (string.IsNullOrEmpty(_user.UserName) || string.IsNullOrEmpty(_user.Password))
                throw new ArgumentException("user name and password must be specified");
        }

        private void ValidateLoggedIn()
        {
            if (!IsUserAuthenticated)
                throw new ArgumentException("user must be authenticated");
        }

        private void ValidateRequestMessage()
        {
            if (_httpRequestProcessor.RequestMessage == null || _httpRequestProcessor.RequestMessage.IsEmpty())
                throw new ArgumentException("API request message null or empty");
        }

        private void LogException(Exception exception)
        {
            _logger?.LogException(exception);
        }

        #endregion
    }
}