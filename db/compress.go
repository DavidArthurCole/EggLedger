package db

import (
	"bytes"
	"compress/gzip"
)

func compress(in []byte) ([]byte, error) {
	var out bytes.Buffer
	w := gzip.NewWriter(&out)
	_, err := w.Write(in)
	if err != nil {
		return nil, err
	}
	if err := w.Close(); err != nil {
		return nil, err
	}
	return out.Bytes(), nil
}

func decompress(in []byte) ([]byte, error) {
	r, err := gzip.NewReader(bytes.NewReader(in))
	if err != nil {
		return nil, err
	}
	var out bytes.Buffer
	_, err = out.ReadFrom(r)
	if err != nil {
		return nil, err
	}
	return out.Bytes(), nil
}

// DecompressPayload decompresses a gzip-compressed payload blob.
func DecompressPayload(data []byte) ([]byte, error) {
	return decompress(data)
}
