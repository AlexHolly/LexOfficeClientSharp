﻿#if !NETFRAMEWORK
using System;
using AndreasReitberger.API.REST;
using AndreasReitberger.API.REST.Interfaces;
#else
using CommunityToolkit.Mvvm.ComponentModel;
#endif

namespace AndreasReitberger.API.LexOffice
{
    // https://developers.lexoffice.io/docs/#lexoffice-api-documentation/?cid=1766
    public partial class LexOfficeClient
#if NETFRAMEWORK
       : ObservableObject
#else
       : RestApiClient
#endif
    {
        #region Error Handling

#if !NETFRAMEWORK
        /// <summary>
        /// Throws the <seealso cref="IRestApiRequestRespone"/> as <seealso cref="Exception"/>
        /// </summary>
        /// <param name="respone">The respone as <seealso cref="IRestApiRequestRespone"/></param>
        /// <param name="methodName">The name of the calling method</param>
        /// <exception cref="Exception"></exception>
        public static void ThrowOnError(IRestApiRequestRespone? respone, string methodName)
        {
            throw new Exception($"Error in '{methodName}' => Result: {respone?.Result} " +
                $"\n- StatusCode: {respone?.EventArgs?.Status}" +
                $"\n- Error: {respone?.EventArgs?.Message}" +
                $"\n- Uri: {respone?.EventArgs?.Uri}",
                respone?.EventArgs?.Exception
                );
        }
#endif
        #endregion
    }
}
