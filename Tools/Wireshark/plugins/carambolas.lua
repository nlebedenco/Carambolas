-- Carambolas Net Protocol 0.1 Dissector For Wireshark
-- Nicolas Carneiro Lebedenco (nicolas@lebedenco.net)
-- Licensed under GPLv3
--
-- To install just copy this file in %APPDATA%\Wireshark\plugins
--
-- TODO: validate CRC32C
-- TODO: validate Ack.Next <= Ack.Last
-- TODO: is it possible to track down the state of each channel to indicate retransmissions ?

local info = {
    version = "0.1.0",
    author = "Nicolas Carneiro Lebedenco (nicolas@lebedenco.net)",
    description = "Dissector for Carambolas Protocol v0.1",
    repository = "https://github.com/nlebedenco/Carambolas",
}

set_plugin_info(info)

-- Packet grammar. The number in parenthesis is the atom size in bytes. Square brackets denote optional elements. Curly brackets denote encrypted elements.
-- 
-- STM(4) PFLAGS(1) <CON | SECCON | ACC | SECACC | DAT | SECDAT | RST | SECRST>
-- 
--    CON ::= SSN(4) MTU(2) MTC(1) MBW(4) CRC(4)
-- SECCON ::= SSN(4) MTU(2) MTC(1) MBW(4) PUBKEY(32) CRC(4)
--    ACC ::= SSN(4) MTU(2) MTC(1) MBW(4) ATM(4) RW(2) ASSN(4) CRC(4)
-- SECACC ::= SSN(4) MTU(2) MTC(1) MBW(4) ATM(4) {RW(2)} PUBKEY(32) NONCE(8) MAC(16)
--    DAT ::= SSN(4) RW(2) MSGS CRC(4)
-- SECDAT ::= {RW(2) MSGS} NONCE(8) MAC(16)
--    RST ::= SSN(4) CRC(4)
-- SECRST ::= PUBKEY(32) NONCE(8) MAC(16)
-- 
--   MSGS ::= MSG [MSG...]
--    MSG ::= MSGFLAGS(1) <ACKACC | ACK | DUPACK | GAP | DUPGAP | SEG | FRAG>
-- ACKACC ::= ATM(4)
--    ACK ::= NEXT(2) ATM(4)
-- DUPACK ::= CNT(2) NEXT(2) ATM(4)
--    GAP ::= NEXT(2) LAST(2) ATM(4)
-- DUPGAP ::= CNT(2) NEXT(2) LAST(2) ATM(4)
--    SEG ::= SEQ(2) RSN(2) SEGLEN(2) PAYLOAD(N)
--   FRAG ::= SEQ(2) RSN(2) SEGLEN(2) FRAGINDEX(1) FRAGLEN(2) PAYLOAD(N)

-- Packet Flags
local PacketFlags = {
    Accept = 0x0A,
    Connect = 0x0C,
    Data = 0x0D,
    Reset = 0x0F,
    SecAccept = 0x1A,
    SecConnect = 0x1C,
    SecData = 0x1D,
    SecReset = 0x1F,
}

local Packet = {
    Header = {Size = 5}, 
    Checksum = {Size = 4 },
}

Packet.Secure = {
    Key = {Size = 32},
    Nonce = {Size = 8},
    Mac = {Size = 16},        
}

Packet.Sizes = {            
    [PacketFlags.Accept] = Packet.Header.Size + 21 + Packet.Checksum.Size,
    [PacketFlags.Connect] = Packet.Header.Size + 11 + Packet.Checksum.Size,
    [PacketFlags.Data] = Packet.Header.Size + 11, -- A minimum data packet constains only an ACKACC (Packet.Checksum.Size intentionally omitted)
    [PacketFlags.Reset] = Packet.Header.Size + 4 + Packet.Checksum.Size,
    [PacketFlags.SecAccept] = Packet.Header.Size + 17 + Packet.Secure.Key.Size + Packet.Secure.Nonce.Size + Packet.Secure.Mac.Size,
    [PacketFlags.SecConnect] = Packet.Header.Size + 11 + Packet.Secure.Key.Size + Packet.Checksum.Size,
    [PacketFlags.SecData] = Packet.Header.Size + 7 + Packet.Secure.Nonce.Size + Packet.Secure.Mac.Size,
    [PacketFlags.SecReset] = Packet.Header.Size + Packet.Secure.Key.Size + Packet.Secure.Nonce.Size + Packet.Secure.Mac.Size,
}

local MessageFlags = {
    AckAccept = 0x8A,        
    Channel = 0x0F, -- bitmask for the channel
    Opcode = 0xF0,  -- bitmask for the opcode
    Ack = 0xA0,    
    DupAck = 0xB0,    
    Gap = 0xE0,
    DupGap = 0xF0,    
    Segment = 0x20,
    ReliableSegment = 0x60,    
    Fragment = 0x30,
    ReliableFragment = 0x70,    
}

local Message = {}
Message.Sizes = {            
    [MessageFlags.AckAccept] = 4,
    [MessageFlags.Ack] = 6,
    [MessageFlags.DupAck] = 8,
    [MessageFlags.Gap] = 8,
    [MessageFlags.DupGap] = 10,
    [MessageFlags.Segment] = 6,
    [MessageFlags.Fragment] = 9,
}


local QoS = {
    [0] = "Unreliable",
    [0x40] = "Reliable",
}

-- Packet Header
local pf_packet_stm = ProtoField.uint32("carambolas.stm", "Source Time", base.DEC)

local pf_secure = ProtoField.none("carambolas.secure", "[Secure]")
local pf_connect = ProtoField.none("carambolas.connect", "[Connect]")
local pf_accept = ProtoField.none("carambolas.accept", "[Accept]")
local pf_messages = ProtoField.none("carambolas.messages", "[Messages]")
local pf_reset = ProtoField.none("carambolas.reset", "[Reset]")

local pf_packet_ssn = ProtoField.uint32("carambolas.ssn", "Source Session", base.HEX)
local pf_packet_rwd = ProtoField.uint16("carambolas.rwd", "Receive Window", base.DEC)
local pf_packet_crc = ProtoField.uint32("carambolas.crc", "Checksum", base.HEX)

local pf_packet_pubkey = ProtoField.bytes("carambolas.pubkey", "Public key")
local pf_packet_encrypted = ProtoField.bytes("carambolas.encrypted", "Encrypted")
local pf_packet_nonce = ProtoField.bytes("carambolas.nonce", "Nonce")
local pf_packet_mac = ProtoField.bytes("carambolas.mac", "MAC")

-- Connect
local pf_connect_mtu = ProtoField.uint16("carambolas.connect.mtu", "Maximum Transmission Unit", base.DEC)
local pf_connect_mtc = ProtoField.uint8("carambolas.connect.mtc", "Maximum Transmmission Channel", base.DEC)
local pf_connect_mbw = ProtoField.uint32("carambolas.connect.mbw", "Maximum Bandwidth", base.DEC)

-- Accept
local pf_accept_mtu = ProtoField.uint16("carambolas.accept.mtu", "Maximum Transmission Unit", base.DEC)
local pf_accept_mtc = ProtoField.uint8("carambolas.accept.mtc", "Maximum Transmmission Channel", base.DEC)
local pf_accept_mbw = ProtoField.uint32("carambolas.accept.mbw", "Maximum Bandwidth", base.DEC)
local pf_accept_atm = ProtoField.uint32("carambolas.accept.atm", "Acceptance Time", base.DEC)
local pf_accept_assn = ProtoField.uint32("carambolas.accept.assn", "Accepted Session", base.HEX)

-- Ack Accept
local pf_ackacc = ProtoField.none("carambolas.ack.accept", "[Ack(Accept)]")

-- Segment/Fragment
local pf_qos = ProtoField.uint8("carambolas.qos", "QoS", base.HEX, QoS)
local pf_chn = ProtoField.uint8("carambolas.chn", "Channel", base.DEC)
local pf_seq = ProtoField.uint16("carambolas.seq", "Sequence Number", base.DEC)
local pf_rsn = ProtoField.uint16("carambolas.rsn", "Reliable Sequence Number", base.DEC)
local pf_seglen = ProtoField.uint16("carambolas.seglen", "Complete Segment Length", base.DEC)
local pf_fragindex = ProtoField.uint8("carambolas.fragindex", "Fragment Index", base.DEC)
local pf_data = ProtoField.bytes("carambolas.data", "Data")
local pf_data_len = ProtoField.uint16("carambolas.data.len", "Length", base.DEC)

local pf_ping = ProtoField.none("carambolas.ping", "[Ping]")
local pf_segment = ProtoField.none("carambolas.segment", "[Segment]")
local pf_fragment = ProtoField.none("carambolas.fragment", "[Fragment]")
local pf_ack = ProtoField.none("carambolas.ack", "[Ack]")

-- Ack
local pf_ack_count = ProtoField.uint16("carambolas.ack.cnt", "Count", base.DEC)
local pf_ack_next = ProtoField.uint16("carambolas.ack.next", "Next", base.DEC)
local pf_ack_last = ProtoField.uint16("carambolas.ack.last", "Last", base.DEC)
local pf_ack_gap = ProtoField.uint16("carambolas.ack.gap", "[Gap]", base.DEC)
local pf_ack_atm = ProtoField.uint32("carambolas.ack.atm", "Acknowledged Time", base.DEC)

-- Some expert info fields
local ef_gap = ProtoExpert.new("carambolas.packet.expert.gap", "Packet contains gap information", expert.group.PROTOCOL, expert.severity.NOTE)
local ef_invalid = ProtoExpert.new("carambolas.packet.expert.invalid", "Packet is invalid or malformed", expert.group.MALFORMED, expert.severity.ERROR)

p_carambolas = Proto("carambolas", "Carambolas Protocol Version 0.1")
p_carambolas.fields = {

    pf_packet_stm,
    
    pf_secure,
    pf_connect,
    pf_accept,
    pf_messages,
    pf_reset,

    pf_packet_ssn,
    pf_packet_rwd,
    pf_packet_crc,

    pf_packet_pubkey,
    pf_packet_encrypted,
    pf_packet_nonce,
    pf_packet_mac,

    pf_connect_mtu,
    pf_connect_mtc,
    pf_connect_mbw,

    pf_accept_mtu,
    pf_accept_mtc,
    pf_accept_mbw,
    pf_accept_atm,
    pf_accept_assn,

    pf_ackacc,

    pf_qos,
    pf_chn,
    pf_seq,
    pf_rsn,
    pf_seglen,
    pf_fragindex,    
    pf_data,
    pf_data_len,

    pf_ping,
    pf_segment,
    pf_fragment,
    pf_ack,

    pf_ack_count,
    pf_ack_next,
    pf_ack_last,
    pf_ack_gap,
    pf_ack_atm,
}

p_carambolas.experts = { 
    ef_gap,
    ef_invalid,
}

function uint(subtree, pf, buf, i, n) 
    subtree:add(pf, buf(i, n), buf(i, n):uint()) 
    return i + n
end

function mask(subtree, pf, buf, i, n, mask) 
    subtree:add(pf, buf(i, n), bit.band(buf(i, n):uint(), mask)) 
end

function bytes(subtree, pf, buf, i, n) 
    subtree:add(pf, buf(i, n)) 
    return i + n
end

function expert(subtree, e)
    subtree:add_proto_expert_info(e)
end

function channel_to_string(tbl)
    local result = "{"
    for k, v in pairs(tbl) do
        -- Check the key type (ignore any numerical keys - assume its an array)
        local ts = type(k)
        if ts == "string" then
            result = result..k.."="
        elseif ts == "number" then
            result = result.."["..k.."]"
        end

        -- Check the value type
        local tv = type(v)
        if tv == "table" then
            result = result..channel_to_string(v)
        elseif tv == "boolean" then
            result = result..tostring(v)
        else
            result = result..v
        end
        result = result..","
    end
    -- Remove leading commas from the result
    if result ~= "" then
        result = result:sub(1, result:len()-1)
    end
    return result.."}"
end

function add_stat(tbl, i, property)
    local stats = tbl[i] or {}
    stats[property] = (stats[property] or 0) + 1 
    tbl[i] = stats
end


function p_carambolas.dissector(buf, pinfo, root)
    pinfo.cols.protocol = p_carambolas.name
    
    local subtree = root:add(p_carambolas, buf(0))    
    local summary = {}    
    local channels = {}
    local has_gaps = false

    local n = buf:len()
    if (n > Packet.Header.Size) then
        local i = uint(subtree, pf_packet_stm, buf, 0, 4)
        local pflags = buf(i, 1):uint(); i = i + 1            
        if (pflags == PacketFlags.Accept and n == Packet.Sizes[PacketFlags.Accept]) then            
            table.insert(summary, "ACC")
            i = uint(subtree, pf_packet_ssn, buf, i, 4)
            local msg = subtree:add(pf_accept, buf(i, n-i))
            i = uint(msg, pf_accept_mtu, buf, i, 2)
            i = uint(msg, pf_accept_mtc, buf, i, 1)
            i = uint(msg, pf_accept_mbw, buf, i, 4)
            i = uint(msg, pf_accept_atm, buf, i, 4)
            i = uint(msg, pf_packet_rwd, buf, i, 2)
            i = uint(msg, pf_accept_assn, buf, i, 4)
            i = uint(subtree, pf_packet_crc, buf, i, 4)
        elseif (pflags == PacketFlags.Connect and n == Packet.Sizes[PacketFlags.Connect]) then
            table.insert(summary, "CON")
            i = uint(subtree, pf_packet_ssn, buf, i, 4)
            local msg = subtree:add(pf_connect, buf(i, n-i))            
            i = uint(msg, pf_connect_mtu, buf, i, 2)
            i = uint(msg, pf_connect_mtc, buf, i, 1)
            i = uint(msg, pf_connect_mbw, buf, i, 4)
            i = uint(subtree, pf_packet_crc, buf, i, 4)
        elseif (pflags == PacketFlags.Data and n > Packet.Sizes[PacketFlags.Data]) then
            i = uint(subtree, pf_packet_ssn, buf, i, 4)
            i = uint(subtree, pf_packet_rwd, buf, i, 2)
            local m = n - i
            while(true) do
                local mflags = buf(i, 1):uint(); i = i + 1         
                if (mflags == MessageFlags.AckAccept and m > Message.Sizes[MessageFlags.AckAccept]) then
                    table.insert(summary, "ACK(ACC)")
                    local msg = subtree:add(pf_ackacc, buf(i, Message.Sizes[MessageFlags.AckAccept]))
                    i = uint(msg, pf_ack_atm, buf, i, 4)
                else                    
                    local channel = bit.band(mflags, MessageFlags.Channel)
                    mflags = bit.band(mflags, MessageFlags.Opcode)
                    if (mflags == MessageFlags.Ack and m > Message.Sizes[MessageFlags.Ack]) then
                        add_stat(channels, channel, "Acks")
                        local msg = subtree:add(pf_ack, buf(i-1, Message.Sizes[MessageFlags.Ack]))                        
                        mask(msg, pf_chn, buf, i-1, 1, MessageFlags.Channel) 
                        i = uint(msg, pf_ack_next, buf, i, 2)
                        i = uint(msg, pf_ack_atm, buf, i, 4)
                    elseif (mflags == MessageFlags.DupAck and m > Message.Sizes[MessageFlags.DupAck]) then
                        add_stat(channels, channel, "Dup Acks")
                        local msg = subtree:add(pf_ack, buf(i-1, Message.Sizes[MessageFlags.DupAck]))
                        mask(msg, pf_chn, buf, i-1, 1, MessageFlags.Channel) 
                        i = uint(msg, pf_ack_count, buf, i, 2)
                        i = uint(msg, pf_ack_next, buf, i, 2)
                        i = uint(msg, pf_ack_atm, buf, i, 4)
                    elseif (mflags == MessageFlags.Gap and m > Message.Sizes[MessageFlags.Gap]) then
                        add_stat(channels, channel, "Gaps")
                        has_gaps = true
                        local msg = subtree:add(pf_ack, buf(i-1, Message.Sizes[MessageFlags.Gap]))
                        mask(msg, pf_chn, buf, i-1, 1, MessageFlags.Channel)                         
                        i = uint(msg, pf_ack_next, buf, i, 2)
                        local from = buf(i, 2):uint()
                        i = uint(msg, pf_ack_last, buf, i, 2)
                        local to = buf(i, 2):uint()                        
                        msg:add(pf_ack_gap, buf(i - 4, 4), (to - from + 1) % 65536)
                        i = uint(msg, pf_ack_atm, buf, i, 4)
                    elseif (mflags == MessageFlags.DupGap and m > Message.Sizes[MessageFlags.DupGap]) then
                        add_stat(channels, channel, "Dup Gaps")
                        has_gaps = true
                        local msg = subtree:add(pf_ack, buf(i-1, Message.Sizes[MessageFlags.DupGap]))
                        mask(msg, pf_chn, buf, i-1, 1, MessageFlags.Channel) 
                        i = uint(msg, pf_ack_count, buf, i, 2)
                        i = uint(msg, pf_ack_next, buf, i, 2)
                        local from = buf(i, 2):uint()
                        i = uint(msg, pf_ack_last, buf, i, 2)
                        local to = buf(i, 2):uint()                        
                        msg:add(pf_ack_gap, buf(i - 4, 4), (to - from + 1) % 65536)
                        i = uint(msg, pf_ack_atm, buf, i, 4)
                    elseif ((mflags == MessageFlags.Segment or mflags == MessageFlags.ReliableSegment) and m > Message.Sizes[MessageFlags.Segment]) then                    
                        local seglen = buf(i + 4, 2):uint()
                        local length = Message.Sizes[MessageFlags.Segment] + seglen
                        if (m > length)  then                             
                            local msg = seglen > 0 and subtree:add(pf_segment, buf(i-1, length))
                                                    or subtree:add(pf_ping, buf(i-1, length))
                            mask(msg, pf_qos, buf, i-1, 1, 0x40)
                            mask(msg, pf_chn, buf, i-1, 1, 0x0F)
                            i = uint(msg, pf_seq, buf, i, 2)
                            i = uint(msg, pf_rsn, buf, i, 2)                               
                            i = uint(msg, pf_data_len, buf, i, 2)                            
                            if (seglen > 0) then 
                                add_stat(channels, channel, "Segments")
                                i = bytes(msg, pf_data, buf, i, seglen)
                            elseif (channel == 0) then
                                table.insert(summary, "PING")
                            end    
                        else 
                            expert(subtree, ef_invalid)
                            break 
                        end                        
                    elseif ((mflags == MessageFlags.Fragment or mflags == MessageFlags.ReliableFragmen) and m > Message.Sizes[MessageFlags.Fragment]) then
                        local fraglen = buf(i + 7, 2):uint()
                        local length = Message.Sizes[MessageFlags.Fragment] + fraglen
                        if (m > length)  then 
                            add_stat(channels, channel, "Fragments")
                            local msg = subtree:add(pf_fragment, buf(i-1, length))
                            mask(msg, pf_qos, buf, i-1, 1, 0x40)
                            mask(msg, pf_chn, buf, i-1, 1, 0x0F)
                            i = uint(msg, pf_seq, buf, i, 2)
                            i = uint(msg, pf_rsn, buf, i, 2)                        
                            i = uint(msg, pf_seglen, buf, i, 2)
                            i = uint(msg, pf_fragindex, buf, i, 1)
                            i = uint(msg, pf_data_len, buf, i, 2)
                            if (fraglen > 0) then 
                                i = bytes(msg, pf_data, buf, i, fraglen)
                            end    
                        else 
                            expert(subtree, ef_invalid)
                            break 
                        end                                                
                    else 
                        expert(subtree, ef_invalid); break
                    end
                end                
                m = n - i
                if (m < Packet.Checksum.Size) then
                    expert(subtree, ef_invalid); break
                elseif (m == Packet.Checksum.Size) then
                    i = uint(subtree, pf_packet_crc, buf, i, 4); break
                end
            end    
        elseif (pflags == PacketFlags.Reset and n == Packet.Sizes[PacketFlags.Reset]) then
            table.insert(summary, "RST")
            i = uint(subtree, pf_packet_ssn, buf, i, 4)
            local msg = subtree:add(pf_reset, buf(i, n-i))
            i = uint(subtree, pf_packet_crc, buf, i, 4)
        elseif (pflags == PacketFlags.SecAccept and n == Packet.Sizes[PacketFlags.SecAccept]) then
            table.insert(summary, "SECACC")
            i = uint(subtree, pf_packet_ssn, buf, i, 4)
            subtree:add(pf_secure, buf(i, n-i))
            local msg = subtree:add(pf_accept, buf(i, n-i))                        
            i = uint(msg, pf_accept_mtu, buf, i, 2)
            i = uint(msg, pf_accept_mtc, buf, i, 1)
            i = uint(msg, pf_accept_mbw, buf, i, 4)
            i = uint(msg, pf_accept_atm, buf, i, 4)
            i = bytes(subtree, pf_packet_encrypted, buf, i, 2)
            i = bytes(subtree, pf_packet_pubkey, buf, i, Packet.Secure.Key.Size)
            i = bytes(subtree, pf_packet_nonce, buf, i, Packet.Secure.Nonce.Size)
            i = bytes(subtree, pf_packet_mac, buf, i, Packet.Secure.Mac.Size)
        elseif (pflags == PacketFlags.SecConnect and n == Packet.Sizes[PacketFlags.SecConnect]) then
            table.insert(summary, "SECCON")
            i = uint(subtree, pf_packet_ssn, buf, i, 4)
            subtree:add(pf_secure, buf(i, n-i))
            local msg = subtree:add(pf_connect, buf(i, n-i))            
            i = uint(msg, pf_connect_mtu, buf, i, 2)
            i = uint(msg, pf_connect_mtc, buf, i, 1)
            i = uint(msg, pf_connect_mbw, buf, i, 4)
            i = bytes(subtree, pf_packet_pubkey, buf, i, Packet.Secure.Key.Size)
            i = uint(subtree, pf_packet_crc, buf, i, 4)
        elseif (pflags == PacketFlags.SecData and n >= Packet.Sizes[PacketFlags.SecData]) then
            table.insert(summary, "SECDAT")
            i = uint(subtree, pf_packet_ssn, buf, i, 4)
            subtree:add(pf_secure, buf(i, n-i))
            i = bytes(subtree, pf_packet_encrypted, buf, i, n - i - Packet.Secure.Nonce.Size - Packet.Secure.Mac.Size)
            i = bytes(subtree, pf_packet_nonce, buf, i, Packet.Secure.Nonce.Size)
            i = bytes(subtree, pf_packet_mac, buf, i, Packet.Secure.Mac.Size)
        elseif (pflags == PacketFlags.SecReset and n == Packet.Sizes[PacketFlags.SecReset]) then
            table.insert(summary, "SECRST")
            subtree:add(pf_secure, buf(i, n-i))
            local msg = subtree:add(pf_reset, buf(i, n-i))
            i = bytes(subtree, pf_packet_pubkey, buf, i, Packet.Secure.Key.Size)
            i = bytes(subtree, pf_packet_nonce, buf, i, Packet.Secure.Nonce.Size)
            i = bytes(subtree, pf_packet_mac, buf, i, Packet.Secure.Mac.Size)
        else 
            expert(subtree, ef_invalid)
        end        
    else 
        expert(subtree, ef_invalid)
    end    

    if (has_gaps) then expert(subtree, ef_gap) end
    
    for k, v in pairs(channels) do
        table.insert(summary, "CH".."["..k.."]"..channel_to_string(v))
    end
        
    if not (next(summary) == nil) then
        pinfo.cols.info:append(" " .. table.concat(summary, ", "))
    end
end

-- Add for use with "Decode As..."
local udp_dissector_table = DissectorTable.get("udp.port")
udp_dissector_table:add_for_decode_as(p_carambolas)
