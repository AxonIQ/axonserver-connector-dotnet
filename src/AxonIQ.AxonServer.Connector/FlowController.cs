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

namespace AxonIQ.AxonServer.Connector;

public class FlowController
{
    private PermitCounter _current;
    
    public FlowController(PermitCount threshold)
    {
        Threshold = threshold;
        _current = PermitCounter.Zero;
    }
    
    public PermitCount Threshold { get; }

    public bool Increment()
    {
        _current = _current.Increment();
        if (Threshold != _current) return false;
        _current = PermitCounter.Zero;
        return true;
    }

    public void Reset()
    {
        _current = PermitCounter.Zero;
    }
}