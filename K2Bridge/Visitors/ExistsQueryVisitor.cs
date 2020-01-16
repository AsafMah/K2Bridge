﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.

namespace K2Bridge.Visitors
{
    using System;
    using K2Bridge.Models.Request.Queries;

    internal partial class ElasticSearchDSLVisitor : IVisitor
    {
        public void Visit(ExistsClause existsClause)
        {
            if (existsClause == null)
            {
                throw new ArgumentException(
                    "Argument cannot be null",
                    nameof(existsClause));
            }

            if (string.IsNullOrEmpty(existsClause.FieldName))
            {
                throw new IllegalClauseException("FieldName must be valid");
            }

            existsClause.KQL = $"{KQLOperators.IsNotNull}({existsClause.FieldName})";
        }
    }
}
