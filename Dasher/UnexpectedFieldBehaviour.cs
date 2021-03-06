#region License
//
// Dasher
//
// Copyright 2015-2017 Drew Noakes
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
// More information about this project is available at:
//
//    https://github.com/drewnoakes/dasher
//
#endregion

namespace Dasher
{
    /// <summary>
    /// An enum whose members define how deserialisers behave when they encounter unexpected fields on complex types,
    /// or unexpected members in unions and enums.
    /// </summary>
    public enum UnexpectedFieldBehaviour
    {
        /// <summary>
        /// Unexpected fields/members raise exceptions.
        /// </summary>
        Throw,

        /// <summary>
        /// Unexpected fields/members are ignored.
        /// </summary>
        Ignore
    }
}