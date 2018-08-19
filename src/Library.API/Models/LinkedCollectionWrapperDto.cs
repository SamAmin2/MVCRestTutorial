﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Library.API.Models
{
    public class LinkedCollectionWrapperDto<T> : LinkedResourceBaseDto
        where T: LinkedResourceBaseDto
    {
        public IEnumerable<T> Value { get; set; }
        public LinkedCollectionWrapperDto(IEnumerable<T> value)
        {
            Value = value;

        }
        
    }
}
