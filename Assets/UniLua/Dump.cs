﻿
using System;
using System.Text;
using System.Collections.Generic;

namespace UniLua
{
	public enum DumpStatus
	{
		OK,
		ERROR,
	}

	public delegate DumpStatus LuaWriter( byte[] bytes, int start, int length );
	internal class DumpState
	{
		public static DumpStatus Dump(
			LuaProto proto, LuaWriter writer, bool strip )
		{
			var d = new DumpState();
			d.Writer 	= writer;
			d.Strip 	= strip;
			d.Status	= DumpStatus.OK;

			d.DumpHeader();
			d.DumpFunction( proto );

			return d.Status;
		}

		private LuaWriter 	Writer;
		private bool		Strip;
		private DumpStatus	Status;

		private const string LUAC_TAIL = "\u0019\u0093\r\n\u001a\n";
		private static int VERSION = (LuaDef.LUA_VERSION_MAJOR[0]-'0') * 16 + 
			(LuaDef.LUA_VERSION_MINOR[0]-'0');
		private static int LUAC_HEADERSIZE = LuaConf.LUA_SIGNATURE.Length +
			2 + 6 + LUAC_TAIL.Length;
		private const int FORMAT = 0;
		private const int ENDIAN = 1;

		private DumpState()
		{
		}

		private byte[] BuildHeader()
		{
			var bytes = new byte[LUAC_HEADERSIZE];
			int i = 0;

			foreach( var b in LuaConf.LUA_SIGNATURE )
				bytes[i++] = (byte)b;

			bytes[i++] = (byte)VERSION;
			bytes[i++] = (byte)FORMAT;
			bytes[i++] = (byte)ENDIAN;
			bytes[i++] = (byte)4; // sizeof(int)
			bytes[i++] = (byte)4; // sizeof(size_t)
			bytes[i++] = (byte)4; // sizeof(Instruction)
			bytes[i++] = (byte)sizeof(double); // sizeof(lua_Number)
			bytes[i++] = (byte)0; // is lua_Number integral?

			foreach( var b in LUAC_TAIL )
				bytes[i++] = (byte)b;

			return bytes;
		}

		private void DumpHeader()
		{
			var bytes = BuildHeader();
			DumpBlock( bytes );
		}

		private void DumpBool( bool value )
		{
			DumpByte( value ? (byte)1 : (byte) 0 );
		}

		private void DumpLuaBoolean( LuaBoolean value )
		{
			DumpBool( value.Value );
		}

		private void DumpInt( int value )
		{
			DumpBlock( BitConverter.GetBytes( value ) );
		}

		private void DumpUInt( uint value )
		{
			DumpBlock( BitConverter.GetBytes( value ) );
		}

		private void DumpLuaNumber( LuaNumber value )
		{
			DumpBlock( BitConverter.GetBytes( value.Value ) );
		}

		private void DumpString( string value )
		{
			if( value == null )
			{
				DumpUInt(0);
			}
			else
			{
				DumpUInt( (uint)(value.Length + 1) );
				foreach( var b in value )
					DumpByte( (byte)b );
				DumpByte( (byte)'\0' );

				// DumpUInt( (uint)(bytes.Length + 1) );
				// var bytes = Encoding.ASCII.GetBytes( value );
				// DumpBlock( bytes );
				// DumpByte( (byte)'\0' );
			}
		}

		private void DumpLuaString( LuaString value )
		{
			DumpString( value.Value );
		}

		private void DumpByte( byte value )
		{
			var bytes = new byte[] { value };
			DumpBlock( bytes );
		}

		private void DumpCode( LuaProto proto )
		{
			DumpVector( proto.Code, (ins) => {
				DumpBlock( BitConverter.GetBytes( (uint)ins ) );
			});
		}

		private void DumpConstants( LuaProto proto )
		{
			DumpVector( proto.K, (k) => {
				var t = k.LuaType;
				DumpByte( (byte)t );
				switch( t )
				{
					case LuaType.LUA_TNIL:
						break;
					case LuaType.LUA_TBOOLEAN:
						DumpLuaBoolean( k as LuaBoolean );
						break;
					case LuaType.LUA_TNUMBER:
						DumpLuaNumber( k as LuaNumber );
						break;
					case LuaType.LUA_TSTRING:
						DumpLuaString( k as LuaString );
						break;
					default:
						Utl.Assert(false);
						break;
				}
			});

			DumpVector( proto.P, (p) => {
				DumpFunction( p );
			});
		}

		private void DumpUpvalues( LuaProto proto )
		{
			DumpVector( proto.Upvalues, (upval) => {
				DumpByte( upval.InStack ? (byte)1 : (byte)0 );
				DumpByte( (byte)upval.Index );
			});
		}

		private void DumpDebug( LuaProto proto )
		{
			DumpString( Strip ? null : proto.Source );

			DumpVector( (Strip ? null : proto.LineInfo), (line) => {
				DumpInt(line);
			});

			DumpVector( (Strip ? null : proto.LocVars), (locvar) => {
				DumpString( locvar.VarName );
				DumpInt( locvar.StartPc );
				DumpInt( locvar.EndPc );
			});

			DumpVector( (Strip ? null : proto.Upvalues), (upval) => {
				DumpString( upval.Name );
			});
		}

		private void DumpFunction( LuaProto proto )
		{
			DumpInt( proto.LineDefined );
			DumpInt( proto.LastLineDefined );
			DumpByte( (byte)proto.NumParams );
			DumpByte( proto.IsVarArg ? (byte)1 : (byte)0 );
			DumpByte( (byte)proto.MaxStackSize );
			DumpCode( proto );
			DumpConstants( proto );
			DumpUpvalues( proto );
			DumpDebug( proto );
		}

		private delegate void DumpItemDelegate<T>( T item );
		private void DumpVector<T>( IList<T> list, DumpItemDelegate<T> dumpItem )
		{
			if( list == null )
			{
				DumpInt( 0 );
			}
			else
			{
				DumpInt( list.Count );
				foreach( var item in list )
				{
					dumpItem( item );
				}
			}
		}

		private void DumpBlock( byte[] bytes )
		{
			DumpBlock( bytes, 0, bytes.Length );
		}

		private void DumpBlock( byte[] bytes, int start, int length )
		{
			if( Status == DumpStatus.OK )
			{
				Status = Writer(bytes, start, length);
			}
		}
	}
}

