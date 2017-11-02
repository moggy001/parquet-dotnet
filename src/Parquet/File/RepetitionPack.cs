﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Parquet.Data;

namespace Parquet.File
{
   /// <summary>
   /// Packs/unpacks repetition levels
   /// </summary>
   class RepetitionPack
   {
      private readonly SchemaElement _schema;
      private readonly ParquetOptions _formatOptions;

      public RepetitionPack(SchemaElement schema, ParquetOptions formatOptions = null)
      {
         _schema = schema;
         _formatOptions = formatOptions ?? new ParquetOptions();
      }

      public IList Unpack(IList hierarchy, out List<int> levels)
      {
         levels = new List<int>();
         IList flatValues = _schema.CreateValuesList(0);

         int touched = 0;
         Unpack(hierarchy, levels, flatValues, ref touched, 0);

         return flatValues;
      }

      private void Unpack(IList list, List<int> levels, IList flatValues, ref int touchedListLevel, int listLevel)
      {
         for (int i = 0; i < list.Count; i++)
         {
            object item = list[i];

            if ((listLevel != _schema.MaxRepetitionLevel) && (item is IList nestedList))
            {
               Unpack(nestedList, levels, flatValues, ref touchedListLevel, listLevel + 1);
            }
            else
            {
               flatValues.Add(item);
               levels.Add(touchedListLevel);
            }

            touchedListLevel = listLevel;
         }
      }

      public IList Pack(IList flatValues, List<int> levels)
      {
         if (levels == null || _schema.MaxRepetitionLevel == 0) return flatValues;

         //horizontal list split
         var values = new List<IList>();
         IList[] hl = new IList[_schema.MaxRepetitionLevel];

         //repetition level indicates where to start to create new lists

         IList chunk = null;
         int lrl = -1;

         for (int i = 0; i < flatValues.Count; i++)
         {
            int rl = levels[i];

            if (lrl != rl)
            {
               CreateLists(hl, rl);
               lrl = rl;
               chunk = hl[hl.Length - 1];

               if (rl == 0)
               {
                  //list at level 0 will be a new element
                  values.Add(hl[0]);
               }
            }

            chunk.Add(flatValues[i]);
         }

         return values;
      }

      private void CreateLists(IList[] hl, int rl)
      {
         int maxIdx = _schema.MaxRepetitionLevel - 1;

         //replace lists in chain with new instances
         for (int i = maxIdx; i >= rl; i--)
         {
            IList nl = (i == maxIdx)
               ? _schema.CreateValuesList(0)
               : new List<IList>();

            hl[i] = nl;
         }

         //rightest old list now should point to leftest new list
         if (rl > 0 && rl <= maxIdx)
         {
            hl[rl - 1].Add(hl[rl]);
         }

         //chain new lists together
         for (int i = maxIdx - 1; i >= rl; i--)
         {
            hl[i].Add(hl[i + 1]);
         }
      }

   }
}
