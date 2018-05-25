﻿using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Stratis.Validators.Net;

namespace Stratis.SmartContracts.Core.ContractValidation
{
    /// <summary>
    /// Validates that <see cref="Mono.Cecil.TypeDefinition"/> inherits from <see cref="Stratis.SmartContracts.SmartContract"/>
    /// </summary>
    public class InheritsSmartContractValidator : ITypeDefinitionValidator
    {
        public static string SmartContractType = typeof(SmartContract).FullName;

        public IEnumerable<FormatValidationError> Validate(TypeDefinition type)
        {
            if (SmartContractType != type.BaseType.FullName)
            {
                return new List<FormatValidationError>{
                    new FormatValidationError("Contract must implement the SmartContract class.")
                };
            }

            return Enumerable.Empty<FormatValidationError>();
        }
    }
}