﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using InstagramApiSharp.API.Processors;
using InstagramApiSharp.Classes;
using InstagramApiSharp.Classes.Models;
using InstagramApiSharp.Classes.ResponseWrappers;

namespace InstagramApiSharp.API
{
    public interface IInstaApi
    {
        #region Properties

        /// <summary>
        ///     Indicates whether user authenticated or not
        /// </summary>
        bool IsUserAuthenticated { get; }

        /// <summary>
        ///     Live api functions
        /// </summary>
        ILiveProcessor LiveProcessor { get; }
        /// <summary>
        ///     Discover api functions.
        /// </summary>
        IDiscoverProcessor DiscoverProcessor { get; }
        /// <summary>
        ///     Account api functions.
        /// </summary>
        IAccountProcessor AccountProcessor { get; }
        /// <summary>
        ///     Story api functions.
        /// </summary>
        IStoryProcessor StoryProcessor { get; }
        /// <summary>
        ///     Media api functions.
        /// </summary>
        IMediaProcessor MediaProcessor { get; }
        /// <summary>
        ///     Comments api functions.
        /// </summary>
        ICommentProcessor CommentProcessor { get; }
        /// <summary>
        ///     Messaging (direct) api functions.
        /// </summary>
        IMessagingProcessor MessagingProcessor { get; }
        /// <summary>
        ///     Feed api functions.
        /// </summary>
        IFeedProcessor FeedProcessor { get; }
        /// <summary>
        ///     Collection api functions.
        /// </summary>
        ICollectionProcessor CollectionProcessor { get; }
        /// <summary>
        ///     Location api functions.
        /// </summary>
        ILocationProcessor LocationProcessor { get; }
        /// <summary>
        ///     Hashtag api functions.
        /// </summary>
        IHashtagProcessor HashtagProcessor { get; }
        /// <summary>
        ///     User api functions.
        /// </summary>
        IUserProcessor UserProcessor { get; }

        #endregion

        UserSessionData GetLoggedUser();
        /// <summary>
        ///     Get current state info as Memory stream
        /// </summary>
        /// <returns>State data</returns>
        Stream GetStateDataAsStream();
        /// <summary>
        ///     Set state data from provided stream
        /// </summary>
        void LoadStateDataFromStream(Stream data);
        /// <summary>
        ///     Get current state info as Json string
        /// </summary>
        /// <returns>State data</returns>
        string GetStateDataAsString();
        /// <summary>
        ///     Set state data from provided json string
        /// </summary>
        void LoadStateDataFromString(string data);


        /// <summary>
        ///     Get challenge require(checkpoint required) options.
        /// </summary>
        /// <returns></returns>
        Task<IResult<ChallengeRequireVerifyMethod>> GetChallengeRequireVerifyMethodAsync();
        Task<IResult<ChallengeRequireVerifyMethod>> ResetChallengeRequireVerifyMethodAsync();
        Task<IResult<ChallengeRequireSMSVerify>> RequestVerifyCodeToSMSForChallengeRequireAsync();
        Task<IResult<ChallengeRequireEmailVerify>> RequestVerifyCodeToEmailForChallengeRequireAsync();
        Task<IResult<ChallengeRequireVerifyCode>> VerifyCodeForChallengeRequireAsync(string verifyCode);
        /// <summary>
        ///     Set cookie and html document to verify login information.
        /// </summary>
        /// <param name="htmlDocument">Html document source</param>
        /// <param name="cookies">Cookies from webview or webbrowser control</param>
        /// <returns>True if logged in, False if not</returns>
        Task<IResult<bool>> SetCookiesAndHtmlForFacebookLoginAsync(string htmlDocument, string cookies ,bool validate = false);
        /// <summary>
        ///     Set cookie and web browser response object to verify login information.
        /// </summary>
        /// <param name="webBrowserResponse">Web browser response object</param>
        /// <param name="cookies">Cookies from webview or webbrowser control</param>
        /// <returns>True if logged in, False if not</returns>
        Task<IResult<bool>> SetCookiesAndHtmlForFacebookLogin(WebBrowserResponse webBrowserResponse, string cookies, bool validate = false);


        #region Async Members
        /// <summary>
        ///     Check email availability
        /// </summary>
        /// <param name="email">Email to check</param>
        Task<IResult<CheckEmailRegistration>> CheckEmailAsync(string email);
        /// <summary>
        ///     Check phone number availability
        /// </summary>
        /// <param name="phoneNumber">Phone number to check</param>
        Task<IResult<InstaDefault>> CheckPhoneNumberAsync(string phoneNumber);
        /// <summary>
        ///     Send sign up sms code
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        Task<IResult<InstaDefault>> SendSignUpSmsCodeAsync(string phoneNumber);
        /// <summary>
        ///     Verify sign up sms code
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        /// <param name="verificationCode">Verification code</param>
        Task<IResult<PhoneNumberRegistration>> VerifySignUpSmsCodeAsync(string phoneNumber, string verificationCode);
        /// <summary>
        ///     Get username suggestions
        /// </summary>
        /// <param name="name">Name</param>
        Task<IResult<RegistrationSuggestionResponse>> GetUsernameSuggestionsAsync(string name);
        /// <summary>
        ///     Validate new account creation with phone number
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        /// <param name="verificationCode">Verification code</param>
        /// <param name="username">Username to set</param>
        /// <param name="password">Password to set</param>
        /// <param name="firstName">First name to set</param>
        Task<IResult<AccountCreation>> ValidateNewAccountWithPhoneNumberAsync(string phoneNumber, string verificationCode, string username, string password, string firstName);
        /// <summary>
        ///     Create a new instagram account
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="password">Password</param>
        /// <param name="email">Email</param>
        /// <param name="firstName">First name (optional)</param>
        Task<IResult<AccountCreation>> CreateNewAccountAsync(string username, string password, string email, string firstName);        
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
        Task<IResult<InstaLoginResult>> LoginAsync(bool isNewLogin = true);

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
        Task<IResult<InstaLoginTwoFactorResult>> TwoFactorLoginAsync(string verificationCode);

        /// <summary>
        ///     Get Two Factor Authentication details
        /// </summary>
        /// <returns>
        ///     An instance of TwoFactorLoginInfo if success.
        ///     A null reference if not success; in this case, do LoginAsync first and check if Two Factor Authentication is
        ///     required, if not, don't run this method
        /// </returns>
        Task<IResult<TwoFactorLoginInfo>> GetTwoFactorInfoAsync();

        /// <summary>
        ///     Logout from instagram asynchronously
        /// </summary>
        /// <returns>True if logged out without errors</returns>
        Task<IResult<bool>> LogoutAsync();
        /// <summary>
        ///     Get currently logged in user info asynchronously
        /// </summary>
        /// <returns>
        ///     <see cref="InstaCurrentUser" />
        /// </returns>
        Task<IResult<InstaCurrentUser>> GetCurrentUserAsync();
        #endregion
    }
}