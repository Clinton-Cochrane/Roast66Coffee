// Services/TwilioService.cs
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

public class TwilioService
{
    private readonly string? _accountSid;
    private readonly string? _authToken;
    private readonly string? _twilioPhoneNumber;
    private readonly string? _messagingServiceSid;
    private readonly string? _statusCallbackUrl;

    public TwilioService(IConfiguration configuration)
    {
        _accountSid = configuration["Twilio:AccountSID"];
        _authToken = configuration["Twilio:AuthToken"];
        _twilioPhoneNumber = configuration["Twilio:FromPhoneNumber"];
        _messagingServiceSid = configuration["Twilio:MessagingServiceSid"];
        _statusCallbackUrl = configuration["Twilio:StatusCallbackUrl"];
        if (!string.IsNullOrEmpty(_accountSid) && !string.IsNullOrEmpty(_authToken))
        {
            TwilioClient.Init(_accountSid, _authToken);
        }
    }

    public bool IsConfigured() =>
        !string.IsNullOrEmpty(_accountSid) &&
        !string.IsNullOrEmpty(_authToken) &&
        (!string.IsNullOrEmpty(_messagingServiceSid) || !string.IsNullOrEmpty(_twilioPhoneNumber));

    public async Task<string?> SendSmsAsync(string toPhoneNumber, string message, string? fromPhoneNumberOverride = null)
    {
        if (!IsConfigured())
        {
            return null;
        }
        var messageOptions = new CreateMessageOptions(new PhoneNumber(toPhoneNumber))
        {
            Body = message
        };

        if (!string.IsNullOrWhiteSpace(_messagingServiceSid))
        {
            messageOptions.MessagingServiceSid = _messagingServiceSid;
        }
        else if (!string.IsNullOrWhiteSpace(fromPhoneNumberOverride))
        {
            messageOptions.From = new PhoneNumber(fromPhoneNumberOverride);
        }
        else if (!string.IsNullOrWhiteSpace(_twilioPhoneNumber))
        {
            messageOptions.From = new PhoneNumber(_twilioPhoneNumber);
        }

        if (!string.IsNullOrWhiteSpace(_statusCallbackUrl) && Uri.IsWellFormedUriString(_statusCallbackUrl, UriKind.Absolute))
        {
            messageOptions.StatusCallback = new Uri(_statusCallbackUrl);
        }

        var created = await MessageResource.CreateAsync(messageOptions);
        return created.Sid;
    }
}
