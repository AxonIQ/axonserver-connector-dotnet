/*
 * Copyright (c) 2022. AxonIQ
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Google.Protobuf.Reflection;

namespace AxonIQ.AxonServer.Connector;

public class AxonServerException : Exception
{
    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message): base(message)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = string.Empty;
        Details = Array.Empty<string>();
    }
    
    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message, string location): base(message)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = location ?? throw new ArgumentNullException(nameof(location));
        Details = Array.Empty<string>();
    }
    
    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message, string location, IReadOnlyCollection<string> details): base(message)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = location ?? throw new ArgumentNullException(nameof(location));
        Details = details ?? throw new ArgumentNullException(nameof(details));
    }

    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message, Exception innerException): base(message, innerException)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = string.Empty;
        Details = Array.Empty<string>();
    }
    
    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message, string location, Exception innerException): base(message, innerException)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = location ?? throw new ArgumentNullException(nameof(location));
        Details = Array.Empty<string>();
    }
    
    public AxonServerException(ClientIdentity clientIdentity, ErrorCategory errorCategory, string message, string location, IReadOnlyCollection<string> details, Exception innerException): base(message, innerException)
    {
        ClientIdentity = clientIdentity ?? throw new ArgumentNullException(nameof(clientIdentity));
        ErrorCategory = errorCategory ?? throw new ArgumentNullException(nameof(errorCategory));
        Location = location ?? throw new ArgumentNullException(nameof(location));
        Details = details ?? throw new ArgumentNullException(nameof(details));
    }
    
    public ClientIdentity ClientIdentity { get; }
    public ErrorCategory ErrorCategory { get; }
    public string Location { get; }
    public IReadOnlyCollection<string> Details { get; }
}